using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using System.Globalization;
using System.IO.Compression;


var builder = WebApplication.CreateBuilder(args);
// Hỗ trợ tự động nhận diện PORT trên Render / Cloud PaaS
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Cấu hình ForwardedHeaders cho Reverse Proxy (Render, Nginx, Docker)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});


// Nén phản hồi
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "text/plain",
        "application/javascript"
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

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ===== 2. Identity =====
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<ApplicationRole>()
.AddSignInManager<SignInManager<ApplicationUser>>()
.AddRoleManager<RoleManager<ApplicationRole>>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ===== 3. Web API, SignalR, Cache, Session =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddLocalization();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();


// ===== 4. CORS (Cho phép React gọi API từ bất kỳ port localhost nào) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

//đăng ký service hoạt động
builder.Services.AddHttpClient();
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.AddSingleton<IImageStorageService, CloudinaryImageStorageService>();


var supportedCultures = new[] { new CultureInfo("vi"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

builder.Services.AddDataProtection();

var app = builder.Build();

// // Khởi tạo Database & Seed dữ liệu ban đầu
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//     await db.Database.MigrateAsync();
//     if (app.Environment.IsDevelopment())
//     {
//         await DbSeeder.SeedAsync(scope.ServiceProvider);
//     }
// }

// // ===== Pipeline xử lý request =====
// if (app.Environment.IsDevelopment())
// {
//     app.UseMigrationsEndPoint();
//     app.MapOpenApi();
// }

app.UseResponseCompression();
app.UseForwardedHeaders();
// Vite gọi backend qua HTTP localhost:5084 trong môi trường phát triển.
// Redirect sang HTTPS làm request proxy rời khỏi origin Vite và bị trình duyệt chặn.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseRouting();

app.UseAuthentication();
// app.Use(async (context, next) =>
// {
//     var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//     if (!string.IsNullOrWhiteSpace(userId))
//     {
//         var presence = context.RequestServices.GetRequiredService<IUserPresenceService>();
//         if (context.Request.Path.StartsWithSegments("/api/auth/logout"))
//         {
//             presence.MarkOffline(userId);
//         }
//         else
//         {
//             presence.TouchUserActivity(userId);
//         }
//     }
//     await next();
// });
app.UseAuthorization();


// Health check endpoint cho Render & UptimeRobot (chống ngủ)
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

// Map Web API Controllers
app.MapControllers();

app.Run();

