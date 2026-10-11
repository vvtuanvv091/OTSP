using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Models;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. Cơ sở dữ liệu (PostgreSQL trên Neon) =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

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

var app = builder.Build();

// ===== 4. Seed dữ liệu mẫu (chỉ khi đang phát triển) =====
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
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
app.UseRouting();

app.UseAuthentication();   // phải đứng trước UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();      // API có [Route] (attribute routing)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()        // trang Identity: /Identity/Account/Login, Register...
   .WithStaticAssets();

app.Run();