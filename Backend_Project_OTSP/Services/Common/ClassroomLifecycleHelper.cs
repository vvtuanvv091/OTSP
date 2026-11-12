using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Common;

public static class ClassroomLifecycleHelper
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    public const int EarlyJoinMinutes = 60;
    public const double ExtraSessionCloseHours = 2.0;
    public const int MaxScheduleHorizonDays = 730;
    public const int RollingPreGenerateWeeks = 12;

    public static DateTime GetVietnamNow(DateTime? utcNow = null)
        => (utcNow ?? DateTime.UtcNow).Add(VietnamOffset);

    public static DateOnly GetVietnamToday(DateTime? utcNow = null)
        => DateOnly.FromDateTime(GetVietnamNow(utcNow));

    public static DateTime ToVietnamTime(DateTime utcTime)
        => DateTime.SpecifyKind(utcTime, DateTimeKind.Utc).Add(VietnamOffset);

    public static DateTime VietnamDateTimeToUtc(DateOnly date, TimeOnly time)
    {
        var vnLocal = date.ToDateTime(time);
        return DateTime.SpecifyKind(vnLocal.Subtract(VietnamOffset), DateTimeKind.Utc);
    }

    public static double NormalizeHoursPerSession(double rawHours)
    {
        if (rawHours <= 0) return 2.0;

        double hours;
        if (rawHours >= 45.0)
            hours = rawHours / 60.0; // Stored in minutes (e.g. 60, 90, 120)
        else if (rawHours >= 10.0)
            hours = rawHours / 10.0; // Legacy FormData locale dot-strip (e.g. 15 -> 1.5, 20 -> 2.0)
        else
            hours = rawHours;

        return Math.Clamp(Math.Round(hours, 2), 0.5, 4.0);
    }

    public static double GetSessionDurationHours(ClassSession session, Classroom? classroom = null)
    {
        if (session.DurationHours.HasValue && session.DurationHours.Value > 0)
            return NormalizeHoursPerSession(session.DurationHours.Value);

        if (classroom != null && classroom.HoursPerSession > 0)
            return NormalizeHoursPerSession(classroom.HoursPerSession);

        return 2.0;
    }

    public static bool IsWithinJoinWindow(
        DateTime scheduledAtUtc,
        SessionStatus status,
        double hoursPerSession,
        DateTime? nowUtc = null)
    {
        if (status == SessionStatus.Done || status == SessionStatus.Cancelled)
            return false;

        if (status == SessionStatus.InProgress)
            return true;

        var currentUtc = nowUtc ?? DateTime.UtcNow;
        var duration = NormalizeHoursPerSession(hoursPerSession);
        var scheduledUtc = DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc);

        return currentUtc >= scheduledUtc.AddMinutes(-EarlyJoinMinutes)
            && currentUtc <= scheduledUtc.AddHours(Math.Max(1.0, duration) + ExtraSessionCloseHours);
    }

    public static bool IntervalsOverlap(DateTime aStartUtc, double aHours, DateTime bStartUtc, double bHours)
    {
        var aStart = DateTime.SpecifyKind(aStartUtc, DateTimeKind.Utc);
        var bStart = DateTime.SpecifyKind(bStartUtc, DateTimeKind.Utc);
        var aEnd = aStart.AddHours(Math.Max(0.5, aHours));
        var bEnd = bStart.AddHours(Math.Max(0.5, bHours));
        return aStart < bEnd && bStart < aEnd;
    }

    public static string FormatDayVn(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ 2",
        DayOfWeek.Tuesday => "Thứ 3",
        DayOfWeek.Wednesday => "Thứ 4",
        DayOfWeek.Thursday => "Thứ 5",
        DayOfWeek.Friday => "Thứ 6",
        DayOfWeek.Saturday => "Thứ 7",
        DayOfWeek.Sunday => "Chủ nhật",
        _ => day.ToString()
    };

    public static string FormatScheduleSummary(IEnumerable<ClassroomWeeklySchedule> schedules)
    {
        var ordered = schedules
            .OrderBy(ws => ws.SessionIndex)
            .ThenBy(ws => ((int)ws.DayOfWeek + 6) % 7)
            .ThenBy(ws => ws.StartTime)
            .Select(ws => $"{FormatDayVn(ws.DayOfWeek)} ({ws.StartTime:HH\\:mm} - {ws.EndTime:HH\\:mm})")
            .ToList();

        return ordered.Count > 0 ? string.Join(", ", ordered) : "Chưa xếp lịch cố định";
    }

    public static List<string> FormatWeeklyScheduleItems(IEnumerable<ClassroomWeeklySchedule> schedules)
    {
        return schedules
            .OrderBy(ws => ws.SessionIndex)
            .ThenBy(ws => ((int)ws.DayOfWeek + 6) % 7)
            .ThenBy(ws => ws.StartTime)
            .Select(ws => $"{FormatDayVn(ws.DayOfWeek)}: {ws.StartTime:HH\\:mm} - {ws.EndTime:HH\\:mm}")
            .ToList();
    }

    public static int CountScheduledOccurrences(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<ClassroomWeeklySchedule> schedules)
    {
        if (endDate < startDate) return 0;
        var scheduleList = schedules.ToList();
        if (scheduleList.Count == 0) return 0;

        var dayCounts = scheduleList
            .GroupBy(s => s.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Count());

        var total = 0;
        var daysSpan = Math.Min(endDate.DayNumber - startDate.DayNumber, MaxScheduleHorizonDays);
        for (var offset = 0; offset <= daysSpan; offset++)
        {
            var d = startDate.AddDays(offset);
            if (dayCounts.TryGetValue(d.DayOfWeek, out var countForDay))
                total += countForDay;
        }
        return total;
    }

    public static DateOnly? CalculateEndDateFromTotalSessions(
        DateOnly startDate,
        int totalSessions,
        IEnumerable<ClassroomWeeklySchedule> schedules)
    {
        if (totalSessions <= 0) return startDate;
        var scheduleList = schedules
            .OrderBy(ws => ws.StartTime)
            .ThenBy(ws => ws.SessionIndex)
            .ToList();

        if (scheduleList.Count == 0)
            return startDate.AddDays((totalSessions - 1) * 7);

        var count = 0;
        for (var offset = 0; offset <= MaxScheduleHorizonDays; offset++)
        {
            var d = startDate.AddDays(offset);
            var daySlots = scheduleList.Count(s => s.DayOfWeek == d.DayOfWeek);
            if (daySlots == 0) continue;

            count += daySlots;
            if (count >= totalSessions)
                return d;
        }

        return startDate.AddDays(MaxScheduleHorizonDays);
    }

    public static int CalculateExpectedTotalSessions(
        DateOnly? startDate,
        DateOnly? endDate,
        int? explicitTotalSessions,
        int sessionsPerWeek,
        IEnumerable<ClassroomWeeklySchedule>? schedules = null)
    {
        if (explicitTotalSessions.HasValue && explicitTotalSessions.Value > 0)
            return explicitTotalSessions.Value;

        var safeSpw = Math.Clamp(sessionsPerWeek, 1, 7);
        if (startDate.HasValue && endDate.HasValue && endDate.Value >= startDate.Value)
        {
            if (schedules != null)
            {
                var counted = CountScheduledOccurrences(startDate.Value, endDate.Value, schedules);
                if (counted > 0) return counted;
            }
            var weeks = Math.Max(1, (int)Math.Ceiling((endDate.Value.DayNumber - startDate.Value.DayNumber + 1) / 7.0));
            return weeks * safeSpw;
        }

        return safeSpw * 4;
    }

    public static bool IsGroupEnrollmentOpen(
        Classroom classroom,
        DateOnly todayVn,
        bool hasStartedOrCompletedSession)
    {
        if (classroom.Status != ClassroomStatus.Active || classroom.Type != ClassroomType.Group)
            return false;

        if (hasStartedOrCompletedSession)
            return false;

        if (classroom.RegistrationDeadline.HasValue)
        {
            if (todayVn > classroom.RegistrationDeadline.Value)
                return false;
            if (classroom.StartDate.HasValue && todayVn > classroom.StartDate.Value)
                return false;
            return true;
        }

        if (classroom.StartDate.HasValue && classroom.StartDate.Value <= todayVn)
            return false;

        return true;
    }

    public static List<(DateTime ScheduledAtUtc, double DurationHours)> BuildPlannedSessions(
        Classroom classroom,
        IEnumerable<ClassroomWeeklySchedule> schedules,
        DateTime minAllowedUtc,
        ICollection<DateTime> existingNonCancelledUtc,
        int maxGenerateHorizonDays = 365)
    {
        var scheduleList = schedules
            .OrderBy(ws => ws.StartTime)
            .ThenBy(ws => ws.SessionIndex)
            .ToList();

        if (scheduleList.Count == 0 || classroom.Status != ClassroomStatus.Active)
            return [];

        var todayVn = GetVietnamToday(minAllowedUtc);
        var startDate = classroom.StartDate ?? todayVn;
        var expectedTotal = CalculateExpectedTotalSessions(
            startDate,
            classroom.EndDate,
            classroom.TotalSessions,
            classroom.SessionsPerWeek,
            scheduleList);

        var remainingQuota = Math.Max(0, expectedTotal - existingNonCancelledUtc.Count);
        if (remainingQuota == 0) return [];

        DateOnly endDate;
        if (classroom.EndDate.HasValue)
        {
            endDate = classroom.EndDate.Value;
        }
        else
        {
            var estimatedEnd = CalculateEndDateFromTotalSessions(startDate, expectedTotal, scheduleList)
                               ?? startDate.AddDays(RollingPreGenerateWeeks * 7);
            var horizonEnd = todayVn.AddDays(maxGenerateHorizonDays);
            endDate = estimatedEnd < horizonEnd ? estimatedEnd : horizonEnd;
        }

        if (endDate < startDate) return [];

        var existingNormalized = new HashSet<long>(
            existingNonCancelledUtc.Select(dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).Ticks / TimeSpan.TicksPerMinute));

        var results = new List<(DateTime ScheduledAtUtc, double DurationHours)>();
        var scanStart = startDate > todayVn.AddDays(-1) ? startDate : todayVn;
        var maxScanDays = Math.Min(endDate.DayNumber - scanStart.DayNumber, MaxScheduleHorizonDays);

        for (var offset = 0; offset <= maxScanDays && results.Count < remainingQuota; offset++)
        {
            var date = scanStart.AddDays(offset);
            foreach (var slot in scheduleList.Where(s => s.DayOfWeek == date.DayOfWeek))
            {
                if (results.Count >= remainingQuota) break;

                var utc = VietnamDateTimeToUtc(date, slot.StartTime);
                if (utc < minAllowedUtc) continue;

                var minuteKey = utc.Ticks / TimeSpan.TicksPerMinute;
                if (existingNormalized.Contains(minuteKey)) continue;

                var slotDuration = (slot.EndTime - slot.StartTime).TotalHours;
                var duration = slotDuration > 0
                    ? NormalizeHoursPerSession(slotDuration)
                    : NormalizeHoursPerSession(classroom.HoursPerSession);

                results.Add((utc, duration));
                existingNormalized.Add(minuteKey);
            }
        }

        return results;
    }
}
