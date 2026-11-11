using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using System.Globalization;
using System.IO.Compression;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Auth;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.Tutor;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Services.Admin;
using BACKEND_PROJECT_OTSP.Services.Admin;
using Backend_Project_OTSP.Services.Sessions;
using Backend_Project_OTSP.Services.Chat;
using Backend_Project_OTSP.Services.Payments;
using Backend_Project_OTSP.Services.Notifications;

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

// ===== 3 Cấu hình JWT Bearer Authentication =====
var jwtKey = builder.Configuration["Jwt:Key"] ?? "OTSP_Super_Secret_Key_For_Jwt_Token_2026_AtLeast32BytesLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Backend_Project_OTSP";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "OTSP_React_App";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };

    // Hỗ trợ truyền Token cho SignalR Hub qua query string (?access_token=...)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },

        // 401: chưa đăng nhập / token hết hạn → trả JSON có thông báo thay vì body rỗng
        OnChallenge = async context =>
        {
            context.HandleResponse();
            if (context.Response.HasStarted) return;

            var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                code = expired ? "TOKEN_EXPIRED" : "UNAUTHORIZED",
                message = expired
                    ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                    : "Bạn cần đăng nhập để thực hiện thao tác này."
            });
        },

        // 403: đã đăng nhập nhưng sai vai trò → trả JSON có thông báo quyền truy cập
        OnForbidden = async context =>
        {
            if (context.Response.HasStarted) return;

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                code = "FORBIDDEN",
                message = "Bạn không có quyền truy cập chức năng này. Vui lòng đăng nhập bằng tài khoản có vai trò phù hợp."
            });
        }
    };
});

builder.Services.AddAuthorization();

// ===== 4. Web API, SignalR, Cache, Session =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Backend_Project_OTSP.Hubs.CustomUserIdProvider>();
builder.Services.AddLocalization();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();


// ===== 5. CORS (Cho phép React gọi API từ bất kỳ port localhost nào) =====
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

//6 đăng ký service hoạt động
builder.Services.AddScoped<IAdminTutorApprovalService, AdminTutorApprovalService>();
builder.Services.AddScoped<IAdminDirectoryService, AdminDirectoryService>();
builder.Services.AddScoped<IAdminConnectionManagementService, AdminConnectionManagementService>();
builder.Services.AddScoped<IAdminClassroomManagementService, AdminClassroomManagementService>();
builder.Services.AddScoped<IAdminLearningDocumentService, AdminLearningDocumentService>();
builder.Services.AddScoped<IAdminOperationsService, AdminOperationsService>();
builder.Services.AddScoped<IStudentProfileService, StudentProfileService>();
builder.Services.AddScoped<IEmailSenderService, EmailSenderService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationDispatcher>(sp => (NotificationService)sp.GetRequiredService<INotificationService>());
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<GoogleIdTokenValidator>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IOpenGroupClassroomService, OpenGroupClassroomService>();
builder.Services.AddScoped<IAvailabilitySlotService, AvailabilitySlotService>();
builder.Services.AddScoped<IConnectionRequestService, ConnectionRequestService>();
builder.Services.AddScoped<IClassSessionService, ClassSessionService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<IChatEventBus, ChatEventBus>();
builder.Services.AddHostedService<ChatNotificationWorker>();
builder.Services.AddHostedService<StaleSessionCleanupWorker>();
builder.Services.AddSingleton<IScheduleBlockService, ScheduleBlockService>();
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.AddSingleton<IImageStorageService, CloudinaryImageStorageService>();
builder.Services.AddSingleton<IUserPresenceService, UserPresenceService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddSingleton<VietQrService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IAiService, AiService>();

builder.Services.AddDataProtection();
//7 build
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

// 8===== Pipeline xử lý request =====
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.MapOpenApi();
}

app.UseResponseCompression();
app.UseForwardedHeaders();
// Vite gọi backend qua HTTP localhost:5084 trong môi trường phát triển.
//  9Redirect sang HTTPS làm request proxy rời khỏi origin Vite và bị trình duyệt chặn.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseRouting();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrWhiteSpace(userId))
    {
        var presence = context.RequestServices.GetRequiredService<IUserPresenceService>();
        if (context.Request.Path.StartsWithSegments("/api/auth/logout"))
        {
            presence.MarkOffline(userId);
            try
            {
                var hubContext = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Backend_Project_OTSP.Hubs.ChatHub>>();
                await hubContext.Clients.All.SendAsync("UserPresenceChanged", userId, false);
            }
            catch { }
        }
        else if (!context.Request.Path.StartsWithSegments("/api/chat/conversations") &&
                 !context.Request.Path.StartsWithSegments("/api/notifications") &&
                 !context.Request.Path.StartsWithSegments("/api/health"))
        {
            presence.TouchUserActivity(userId);
        }
    }
    await next();
});


app.UseAuthorization();

// SignalR Hubs
app.MapHub<ConnectionRequestHub>("/hubs/connection-requests");
app.MapHub<ChatHub>("/hubs/chat");

// Health check endpoint cho Render & UptimeRobot (chống ngủ)
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

// Map Web API Controllers
app.MapControllers();

app.Run();

