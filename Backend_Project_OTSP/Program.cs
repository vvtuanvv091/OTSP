using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
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
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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


// ===== 5. CORS & Rate Limiting =====
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var configuredCsv = builder.Configuration["Cors:AllowedOriginsCsv"];
var frontendUrl = builder.Configuration["App:FrontendUrl"];

var allowedOriginSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var origin in configuredOrigins)
{
    if (!string.IsNullOrWhiteSpace(origin)) allowedOriginSet.Add(origin.Trim().TrimEnd('/'));
}
if (!string.IsNullOrWhiteSpace(configuredCsv))
{
    foreach (var origin in configuredCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        allowedOriginSet.Add(origin.TrimEnd('/'));
    }
}
if (!string.IsNullOrWhiteSpace(frontendUrl))
{
    allowedOriginSet.Add(frontendUrl.Trim().TrimEnd('/'));
}

var isDevelopmentEnv = builder.Environment.IsDevelopment();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrWhiteSpace(origin)) return false;
                  var normalized = origin.TrimEnd('/');
                  if (allowedOriginSet.Contains(normalized)) return true;

                  if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) return false;
                  if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                  {
                      return true;
                  }

                  if (isDevelopmentEnv &&
                      (uri.Host.EndsWith(".ngrok-free.dev", StringComparison.OrdinalIgnoreCase)
                       || uri.Host.EndsWith(".ngrok-free.app", StringComparison.OrdinalIgnoreCase)
                       || uri.Host.EndsWith(".ngrok.io", StringComparison.OrdinalIgnoreCase)))
                  {
                      return true;
                  }

                  // Cho phép Vercel preview/production domain nếu chưa khóa Cors:AllowedOrigins cụ thể
                  if (allowedOriginSet.Count == 0
                      && uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                      && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                  {
                      return true;
                  }

                  return false;
              })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.HttpContext.Response.HasStarted) return;
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Bạn thao tác quá nhanh. Vui lòng chờ một lát rồi thử lại."
        }, token);
    };

    static string ResolveClientKey(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("Auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{ResolveClientKey(http)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("AiAgent", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"ai:{ResolveClientKey(http)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("Webhook", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"webhook:{ResolveClientKey(http)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2,
                AutoReplenishment = true
            }));
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
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddHostedService<EmailQueueWorker>();
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
builder.Services.AddHttpClient("Gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddHttpClient<IAiService, AiService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDataProtection();
//7 build
var app = builder.Build();

// Khởi tạo Database & Seed dữ liệu ban đầu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""AuditLogs"" ALTER COLUMN ""UserId"" DROP NOT NULL;
            ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(100) NULL;
            ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Detail"" text NULL;
            ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""UserAgent"" character varying(500) NULL;

            CREATE TABLE IF NOT EXISTS ""WebVisits"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""DeviceId"" character varying(128) NOT NULL,
                ""VisitDate"" date NOT NULL,
                ""FirstVisitedAt"" timestamp with time zone NOT NULL,
                ""LastVisitedAt"" timestamp with time zone NOT NULL,
                ""HitCount"" integer NOT NULL DEFAULT 1,
                ""FirstPath"" character varying(200) NULL,
                ""IpAddress"" character varying(100) NULL,
                ""UserAgent"" character varying(500) NULL,
                ""UserId"" text NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WebVisits_DeviceId_VisitDate"" ON ""WebVisits"" (""DeviceId"", ""VisitDate"");
            CREATE INDEX IF NOT EXISTS ""IX_WebVisits_UserId"" ON ""WebVisits"" (""UserId"");
            CREATE INDEX IF NOT EXISTS ""IX_WebVisits_VisitDate"" ON ""WebVisits"" (""VisitDate"");
        ");
    }
    catch { }
    if (app.Environment.IsDevelopment())
    {
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }
}

// 8===== Pipeline xử lý request =====
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(feature.Error, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        if (context.Response.HasStarted) return;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."
        });
    });
});

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
app.UseRateLimiter();

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

