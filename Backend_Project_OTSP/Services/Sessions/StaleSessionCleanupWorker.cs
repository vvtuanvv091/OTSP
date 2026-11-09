using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Sessions;

/// <summary>
/// Tự động đánh dấu hoàn thành (Done) các buổi học bị treo quá 6 giờ kể từ giờ bắt đầu
/// mà gia sư quên bấm "Kết thúc buổi học", đồng thời ghi giờ rời cho các thành viên còn lại.
/// </summary>
public sealed class StaleSessionCleanupWorker : BackgroundService
{
    private const double MaxSessionOpenHours = 6;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StaleSessionCleanupWorker> _logger;

    public StaleSessionCleanupWorker(
        IServiceProvider serviceProvider,
        ILogger<StaleSessionCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ ứng dụng khởi động xong rồi mới quét lần đầu
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var threshold = DateTime.UtcNow.AddHours(-MaxSessionOpenHours);

                var staleIds = await db.ClassSessions
                    .Where(s => (s.Status == SessionStatus.InProgress || s.Status == SessionStatus.Scheduled)
                                && s.ScheduledAt < threshold)
                    .Select(s => s.Id)
                    .ToListAsync(stoppingToken);

                if (staleIds.Count > 0)
                {
                    await db.ClassSessions
                        .Where(s => staleIds.Contains(s.Id))
                        .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, SessionStatus.Done), stoppingToken);

                    await db.ClassSessionAttendances
                        .Where(a => staleIds.Contains(a.ClassSessionId) && a.LeftAtUtc == null)
                        .ExecuteUpdateAsync(u => u.SetProperty(a => a.LeftAtUtc, DateTime.UtcNow), stoppingToken);

                    _logger.LogInformation("Đã tự động kết thúc {Count} buổi học quá {Hours} giờ.", staleIds.Count, MaxSessionOpenHours);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Lỗi khi dọn dẹp buổi học quá hạn.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

