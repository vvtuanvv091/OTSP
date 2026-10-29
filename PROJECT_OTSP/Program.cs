using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using PROJECT_OTSP.Services.Events;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<IChatEventBus, ChatEventBus>();
builder.Services.AddHostedService<ChatNotificationWorker>();
builder.Services.AddSingleton<IScheduleBlockService, ScheduleBlockService>();
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.AddSingleton<IImageStorageService, CloudinaryImageStorageService>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();
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
