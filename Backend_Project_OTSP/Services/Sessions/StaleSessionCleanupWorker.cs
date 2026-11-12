using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Sessions;

/// <summary>
/// Tự động xử lý buổi học quá hạn:
/// - InProgress quá 6 giờ (đã mở lớp nhưng quên bấm Kết thúc) -> Done & đóng điểm danh.
/// - Scheduled quá 6 giờ (chưa từng mở lớp) -> Cancelled để không tính nhầm thành Đã học.
/// - Tự động chuyển các lớp Active sang Completed nếu đã hoàn thành đủ số buổi hoặc qua EndDate và không còn buổi sắp tới.
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
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await ProcessStaleSessionsAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Lỗi khi dọn dẹp buổi học quá hạn.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task ProcessStaleSessionsAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var threshold = nowUtc.AddHours(-MaxSessionOpenHours);
        var todayVn = ClassroomLifecycleHelper.GetVietnamToday(nowUtc);

        var staleSessions = await db.ClassSessions
            .Where(s => (s.Status == SessionStatus.InProgress || s.Status == SessionStatus.Scheduled)
                        && s.ScheduledAt < threshold)
            .ToListAsync(cancellationToken);

        var affectedClassroomIds = new HashSet<int>();
        var staleInProgressIds = new List<int>();
        var autoDoneCount = 0;
        var autoCancelledCount = 0;

        foreach (var session in staleSessions)
        {
            affectedClassroomIds.Add(session.ClassroomId);
            if (session.Status == SessionStatus.InProgress)
            {
                session.Status = SessionStatus.Done;
                staleInProgressIds.Add(session.Id);
                autoDoneCount++;
            }
            else if (session.Status == SessionStatus.Scheduled)
            {
                session.Status = SessionStatus.Cancelled;
                autoCancelledCount++;
            }
        }

        if (staleInProgressIds.Count > 0)
        {
            var openAttendances = await db.ClassSessionAttendances
                .Where(a => staleInProgressIds.Contains(a.ClassSessionId) && a.LeftAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var att in openAttendances)
                att.LeftAtUtc = nowUtc;
        }

        if (staleSessions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Đã dọn dẹp buổi học quá {Hours} giờ: {DoneCount} buổi InProgress -> Done, {CancelCount} buổi Scheduled chưa mở -> Cancelled.",
                MaxSessionOpenHours, autoDoneCount, autoCancelledCount);
        }

        var endedActiveIds = await db.Classrooms.AsNoTracking()
            .Where(c => c.Status == ClassroomStatus.Active && c.EndDate.HasValue && c.EndDate.Value < todayVn)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in endedActiveIds)
            affectedClassroomIds.Add(id);

        if (affectedClassroomIds.Count > 0)
        {
            await ClassSessionService.TryAutoCompleteClassroomsAsync(db, affectedClassroomIds, cancellationToken);
        }
    }
}

