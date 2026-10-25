using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Services;

public sealed class AvailabilitySlotService : IAvailabilitySlotService
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;

    public AvailabilitySlotService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<TutorAvailableSlotDto>> GetAvailableSlotsAsync(
        int tutorId,
        int daysAhead = 14,
        CancellationToken cancellationToken = default)
    {
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours));
        var startDateVn = todayVn.AddDays(1);
        var endDateVn = todayVn.AddDays(daysAhead);

        var availabilities = await _db.TutorAvailabilities
            .AsNoTracking()
            .Where(a => a.TutorId == tutorId)
            .OrderBy(a => a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

        if (availabilities.Count == 0)
            return [];

        var windowStartUtc = startDateVn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-VietnamUtcOffsetHours);
        var windowEndUtc = endDateVn.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-VietnamUtcOffsetHours);

        // Lấy các khoảng giờ bận từ các buổi học đã lên lịch
        var busySessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.TutorId == tutorId
                     && s.Classroom.Status == ClassroomStatus.Active
                     && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress)
                     && s.ScheduledAt >= windowStartUtc && s.ScheduledAt < windowEndUtc)
            .Select(s => new
            {
                StartUtc = s.ScheduledAt,
                DurationHours = s.Classroom.HoursPerSession > 0 ? s.Classroom.HoursPerSession : 2.0
            })
            .ToListAsync(cancellationToken);

        // Lấy các khoảng giờ bận từ lịch học thử đã chốt
        var busyTrials = await _db.TrialSchedules
            .AsNoTracking()
            .Where(t => t.ConnectionRequest.TutorId == tutorId
                     && t.Status == ScheduleStatus.Confirmed
                     && t.ScheduledAt >= windowStartUtc && t.ScheduledAt < windowEndUtc)
            .Select(t => new
            {
                StartUtc = t.ScheduledAt,
                DurationHours = 2.0
            })
            .ToListAsync(cancellationToken);

        var busyIntervals = busySessions
            .Select(s => (Start: s.StartUtc, End: s.StartUtc.AddHours(s.DurationHours)))
            .Concat(busyTrials.Select(t => (Start: t.StartUtc, End: t.StartUtc.AddHours(t.DurationHours))))
            .ToList();

        var result = new List<TutorAvailableSlotDto>();
        var nowUtc = DateTime.UtcNow;

        for (var d = startDateVn; d <= endDateVn; d = d.AddDays(1))
        {
            var matchingAvails = availabilities.Where(a => a.DayOfWeek == d.DayOfWeek);
            foreach (var a in matchingAvails)
            {
                var slotStartUtc = DateTime.SpecifyKind(
                    d.ToDateTime(a.StartTime).AddHours(-VietnamUtcOffsetHours),
                    DateTimeKind.Utc);

                var slotEndUtc = DateTime.SpecifyKind(
                    d.ToDateTime(a.EndTime).AddHours(-VietnamUtcOffsetHours),
                    DateTimeKind.Utc);

                if (slotStartUtc <= nowUtc)
                    continue;

                // Thuật toán kiểm tra xung đột thời gian (Interval Overlap Check):
                // Hai khoảng thời gian giao nhau khi và chỉ khi: slotStart < busyEnd VÀ busyStart < slotEnd
                bool isConflict = busyIntervals.Any(busy =>
                    slotStartUtc < busy.End && busy.Start < slotEndUtc);

                if (!isConflict)
                {
                    result.Add(new TutorAvailableSlotDto
                    {
                        SlotUtc = slotStartUtc,
                        DateLabel = $"{ToVnDay(d.DayOfWeek)}, {d:dd/MM/yyyy}",
                        TimeRange = $"{a.StartTime:HH\\:mm} – {a.EndTime:HH\\:mm}",
                        DayOfWeek = d.DayOfWeek,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime
                    });
                }
            }
        }

        return result.OrderBy(r => r.SlotUtc).ToList();
    }

    private static string ToVnDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => day.ToString()
    };
}
