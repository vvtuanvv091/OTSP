using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using PROJECT_OTSP.Services.Events;
using System.Globalization;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// Hỗ trợ tự động nhận diện PORT trên Render / Cloud PaaS
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Cấu hình ForwardedHeaders để tương thích hoàn toàn với SSL Reverse Proxy của Render
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Tối ưu nén phản hồi Brotli + Gzip giúp tải trang nhanh hơn 60-80%
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "text/css",
        "application/javascript",
        "text/javascript",
        "image/svg+xml"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// ===== 1. Cơ sở dữ liệu (PostgreSQL trên Neon) =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    });
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// Hiển thị lỗi migration chi tiết khi đang phát triển
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ===== 2. Identity: dùng ApplicationUser và ApplicationRole =====
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
        options.SignIn.RequireConfirmedAccount = false)   // không bắt xác nhận email
    .AddRoles<ApplicationRole>()                          // bật phân quyền theo vai trò
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ===== 3. MVC, Razor Pages, API =====
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddLocalization();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ===== 3.1 Services =====
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IOpenGroupClassroomService, OpenGroupClassroomService>();
builder.Services.AddScoped<IAvailabilitySlotService, AvailabilitySlotService>();
builder.Services.AddScoped<IConnectionRequestService, ConnectionRequestService>();
builder.Services.AddScoped<IClassSessionService, ClassSessionService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<IChatEventBus, ChatEventBus>();
builder.Services.AddHostedService<ChatNotificationWorker>();
builder.Services.AddSingleton<IScheduleBlockService, ScheduleBlockService>();
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.AddSingleton<IImageStorageService, CloudinaryImageStorageService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddSingleton<VietQrService>();
builder.Services.AddHttpClient();

var supportedCultures = new[] { new CultureInfo("vi"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// Khởi tạo Database & Seed dữ liệu ban đầu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
    {
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }
}

// ===== 5. Pipeline xử lý request =====
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Bật nén phản hồi trước khi gửi về client
app.UseResponseCompression();

// ForwardedHeaders bắt buộc phải nằm trước UseHttpsRedirection khi chạy trên Render
app.UseForwardedHeaders();

app.UseHttpsRedirection();

// Cấu hình lưu bộ nhớ đệm (Browser Cache) mạnh mẽ cho file tĩnh (30 ngày)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable";
        }
    }
});
app.UseRequestLocalization();

app.UseRouting();
app.UseSession();

app.UseAuthentication();   // phải đứng trước UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();      // API có [Route] (attribute routing)
app.MapHub<ConnectionRequestHub>("/hubs/connection-requests");
app.MapHub<ChatHub>("/hubs/chat");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()        // trang Identity: /Identity/Account/Login, Register...
   .WithStaticAssets();

app.Run();
