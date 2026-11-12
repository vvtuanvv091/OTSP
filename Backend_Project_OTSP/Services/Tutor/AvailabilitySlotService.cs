using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Tutor;

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

        var windowStartUtc = startDateVn.AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-VietnamUtcOffsetHours);
        var windowEndUtc = endDateVn.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-VietnamUtcOffsetHours);

        // Lấy lịch học cố định hàng tuần của các lớp học chính thức đang hoạt động của gia sư (cả lớp 1-1 và lớp nhóm)
        var activeClassroomWeeklySchedules = await _db.ClassroomWeeklySchedules
            .AsNoTracking()
            .Where(w => w.Classroom.TutorId == tutorId
                     && w.Classroom.Status == ClassroomStatus.Active)
            .Select(w => new
            {
                w.DayOfWeek,
                w.StartTime,
                w.EndTime,
                w.Classroom.StartDate,
                w.Classroom.EndDate
            })
            .ToListAsync(cancellationToken);

        // Lấy các khoảng giờ bận từ các buổi học đã lên lịch
        var busySessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.TutorId == tutorId
                     && s.Classroom.Status == ClassroomStatus.Active
                     && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress)
                     && s.ScheduledAt >= windowStartUtc.AddHours(-4) && s.ScheduledAt < windowEndUtc)
            .Select(s => new
            {
                StartUtc = s.ScheduledAt,
                s.DurationHours,
                ClassroomHours = s.Classroom.HoursPerSession
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
            .Select(s =>
            {
                var dur = ClassroomLifecycleHelper.NormalizeHoursPerSession(
                    s.DurationHours.HasValue && s.DurationHours.Value > 0 ? s.DurationHours.Value : s.ClassroomHours);
                return (Start: s.StartUtc, End: s.StartUtc.AddHours(dur));
            })
            .Concat(busyTrials.Select(t => (Start: t.StartUtc, End: t.StartUtc.AddHours(t.DurationHours))))
            .ToList();

        // Thêm các khung giờ bận từ lịch học cố định hàng tuần của các lớp chính thức theo đúng [StartDate..EndDate]
        if (activeClassroomWeeklySchedules.Count > 0)
        {
            for (var d = startDateVn.AddDays(-1); d <= endDateVn; d = d.AddDays(1))
            {
                var daySchedules = activeClassroomWeeklySchedules.Where(w =>
                    w.DayOfWeek == d.DayOfWeek
                    && ScheduleConflictHelper.DateRangesOverlap(w.StartDate, w.EndDate, d, d));
                foreach (var w in daySchedules)
                {
                    var busyStartUtc = DateTime.SpecifyKind(
                        d.ToDateTime(w.StartTime).AddHours(-VietnamUtcOffsetHours),
                        DateTimeKind.Utc);

                    var busyEndUtc = w.EndTime > w.StartTime
                        ? DateTime.SpecifyKind(d.ToDateTime(w.EndTime).AddHours(-VietnamUtcOffsetHours), DateTimeKind.Utc)
                        : DateTime.SpecifyKind(d.AddDays(1).ToDateTime(w.EndTime).AddHours(-VietnamUtcOffsetHours), DateTimeKind.Utc);

                    busyIntervals.Add((Start: busyStartUtc, End: busyEndUtc));
                }
            }
        }

        busyIntervals = busyIntervals.OrderBy(i => i.Start).ToList();

        // 1. Thuật toán gộp khoảng bận (Interval Merging Algorithm - O(N log N))
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
        var seenSlotKeys = new HashSet<DateTime>();
        var nowUtc = DateTime.UtcNow;

        for (var d = startDateVn; d <= endDateVn; d = d.AddDays(1))
        {
            var matchingAvails = availabilities.Where(a => a.DayOfWeek == d.DayOfWeek);
            foreach (var a in matchingAvails)
            {
                var availabilityDuration = a.EndTime > a.StartTime
                    ? a.EndTime - a.StartTime
                    : a.EndTime - a.StartTime + TimeSpan.FromDays(1);

                var availHours = availabilityDuration.TotalHours;
                if (availHours < 2.0)
                    continue;

                // Tách khung rảnh >= 4h thành từng ca 2h riêng biệt để không mất ca học thứ 2
                for (var offsetHours = 0.0; offsetHours + 2.0 <= availHours + 0.001; offsetHours += 2.0)
                {
                    var slotStartVn = d.ToDateTime(a.StartTime).AddHours(offsetHours);
                    var slotStartUtc = DateTime.SpecifyKind(
                        slotStartVn.AddHours(-VietnamUtcOffsetHours),
                        DateTimeKind.Utc);
                    var slotEndUtc = slotStartUtc.AddHours(2);

                    if (slotStartUtc <= nowUtc || seenSlotKeys.Contains(slotStartUtc))
                        continue;

                    // 2. Binary Search O(log M)
                    bool isConflict = HasConflict(mergedBusy, slotStartUtc, slotEndUtc);
                    if (!isConflict)
                    {
                        var slotStartTime = TimeOnly.FromDateTime(slotStartVn);
                        var slotEndTime = slotStartTime.AddHours(2);
                        seenSlotKeys.Add(slotStartUtc);
                        result.Add(new TutorAvailableSlotDto
                        {
                            SlotUtc = slotStartUtc,
                            DateLabel = $"{ToVnDay(d.DayOfWeek)}, {d:dd/MM/yyyy}",
                            TimeRange = $"{slotStartTime:HH\\:mm} – {slotEndTime:HH\\:mm}",
                            DayOfWeek = d.DayOfWeek,
                            StartTime = slotStartTime,
                            EndTime = slotEndTime
                        });
                    }
                }
            }
        }

        return result.OrderBy(r => r.SlotUtc).ToList();
    }

    public async Task<List<TutorAvailableSlotDto>> GetAvailableSlotsForStudentAsync(
        int tutorId,
        int studentId,
        int daysAhead = 14,
        CancellationToken cancellationToken = default)
    {
        var slots = await GetAvailableSlotsAsync(tutorId, daysAhead, cancellationToken);
        if (slots.Count == 0)
            return slots;

        var startDateVn = DateOnly.FromDateTime(slots[0].SlotUtc.AddHours(VietnamUtcOffsetHours));
        var windowStartUtc = startDateVn.AddDays(-1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .AddHours(-VietnamUtcOffsetHours);
        var windowEndUtc = slots[^1].SlotUtc.AddHours(2);
        var endDateVn = DateOnly.FromDateTime(windowEndUtc.AddHours(VietnamUtcOffsetHours));

        var studentWeeklySchedules = await _db.ClassroomWeeklySchedules
            .AsNoTracking()
            .Where(w => w.Classroom.Status == ClassroomStatus.Active
                     && w.Classroom.ClassroomStudents.Any(cs => cs.StudentId == studentId))
            .Select(w => new
            {
                w.DayOfWeek,
                w.StartTime,
                w.EndTime,
                w.Classroom.StartDate,
                w.Classroom.EndDate
            })
            .ToListAsync(cancellationToken);

        var busyIntervals = new List<(DateTime Start, DateTime End)>();
        if (studentWeeklySchedules.Count > 0)
        {
            for (var date = startDateVn.AddDays(-1); date <= endDateVn; date = date.AddDays(1))
            {
                foreach (var schedule in studentWeeklySchedules.Where(s =>
                             s.DayOfWeek == date.DayOfWeek
                             && ScheduleConflictHelper.DateRangesOverlap(s.StartDate, s.EndDate, date, date)))
                {
                    var start = DateTime.SpecifyKind(
                        date.ToDateTime(schedule.StartTime).AddHours(-VietnamUtcOffsetHours),
                        DateTimeKind.Utc);
                    var endDate = schedule.EndTime > schedule.StartTime ? date : date.AddDays(1);
                    var end = DateTime.SpecifyKind(
                        endDate.ToDateTime(schedule.EndTime).AddHours(-VietnamUtcOffsetHours),
                        DateTimeKind.Utc);
                    busyIntervals.Add((start, end));
                }
            }
        }

        var busySessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.Status == ClassroomStatus.Active
                     && s.Classroom.ClassroomStudents.Any(cs => cs.StudentId == studentId)
                     && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress)
                     && s.ScheduledAt >= windowStartUtc.AddHours(-4) && s.ScheduledAt < windowEndUtc)
            .Select(s => new
            {
                StartUtc = s.ScheduledAt,
                s.DurationHours,
                ClassroomHours = s.Classroom.HoursPerSession
            })
            .ToListAsync(cancellationToken);

        var busyTrials = await _db.TrialSchedules
            .AsNoTracking()
            .Where(t => t.ConnectionRequest.StudentId == studentId
                     && t.Status == ScheduleStatus.Confirmed
                     && t.ScheduledAt >= windowStartUtc && t.ScheduledAt < windowEndUtc)
            .Select(t => t.ScheduledAt)
            .ToListAsync(cancellationToken);

        busyIntervals.AddRange(busySessions.Select(s =>
        {
            var dur = ClassroomLifecycleHelper.NormalizeHoursPerSession(
                s.DurationHours.HasValue && s.DurationHours.Value > 0 ? s.DurationHours.Value : s.ClassroomHours);
            return (s.StartUtc, s.StartUtc.AddHours(dur));
        }));
        busyIntervals.AddRange(busyTrials.Select(start =>
            (start, start.AddHours(2))));

        return slots.Where(slot =>
        {
            var slotEndUtc = slot.SlotUtc.AddHours(2);
            return !busyIntervals.Any(busy => slot.SlotUtc < busy.End && busy.Start < slotEndUtc);
        }).ToList();
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
