using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;

namespace PROJECT_OTSP.Tests.Helpers;

/// <summary>
/// Factory pattern tạo ApplicationDbContext In-Memory độc lập cho từng bài test,
/// đảm bảo các bài test không bị xung đột dữ liệu (Isolated Tests).
/// </summary>
public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

