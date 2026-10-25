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
            .OrderBy(i => i.Start)
            .ToList();

        // 1. Thuật toán gộp khoảng bận (Interval Merging Algorithm - O(N log N))
        // Gộp các khoảng thời gian bận trùng lặp hoặc gối đầu nhau thành danh sách các khoảng rời rạc duy nhất
        var mergedBusy = new List<(DateTime Start, DateTime End)>(busyIntervals.Count);
        foreach (var interval in busyIntervals)
        {
            if (mergedBusy.Count == 0 || mergedBusy[^1].End < interval.Start)
            {
                mergedBusy.Add(interval);
            }
            else if (mergedBusy[^1].End < interval.End)
            {
                mergedBusy[^1] = (mergedBusy[^1].Start, interval.End);
            }
        }

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

                // 2. Thuật toán Binary Search kiểm tra giao khoảng thời gian (O(log M) thay vì O(M)):
                // Nhanh hơn vượt trội so với quét tuần tự (Linear Scan)
                bool isConflict = HasConflict(mergedBusy, slotStartUtc, slotEndUtc);

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

    /// <summary>
    /// Thuật toán tìm kiếm nhị phân (Binary Search O(log M)) xác định xem slot có xung đột với khoảng bận nào không.
    /// </summary>
    private static bool HasConflict(List<(DateTime Start, DateTime End)> sortedIntervals, DateTime slotStart, DateTime slotEnd)
    {
        if (sortedIntervals.Count == 0) return false;

        int low = 0, high = sortedIntervals.Count - 1;
        int candidateIndex = sortedIntervals.Count;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (sortedIntervals[mid].End > slotStart)
            {
                candidateIndex = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        if (candidateIndex < sortedIntervals.Count)
        {
            return sortedIntervals[candidateIndex].Start < slotEnd;
        }

        return false;
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
