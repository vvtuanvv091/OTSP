using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Common;

public static class ScheduleConflictHelper
{
    public sealed record ScheduleSlotCandidate(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    public static bool DateRangesOverlap(
        DateOnly? aStart,
        DateOnly? aEnd,
        DateOnly bStart,
        DateOnly? bEnd)
    {
        var effAStart = aStart ?? DateOnly.MinValue;
        var effAEnd = aEnd ?? DateOnly.MaxValue;
        var effBEnd = bEnd ?? DateOnly.MaxValue;
        return effAStart <= effBEnd && bStart <= effAEnd;
    }

    public static bool TimesOverlap(TimeOnly aStart, TimeOnly aEnd, TimeOnly bStart, TimeOnly bEnd)
        => aStart < bEnd && bStart < aEnd;

    public static string? FindSelfOverlap(IReadOnlyList<ScheduleSlotCandidate> slots)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].StartTime >= slots[i].EndTime)
                return $"Giờ bắt đầu phải trước giờ kết thúc ({ClassroomLifecycleHelper.FormatDayVn(slots[i].DayOfWeek)}).";

            for (var j = i + 1; j < slots.Count; j++)
            {
                if (slots[i].DayOfWeek == slots[j].DayOfWeek
                    && TimesOverlap(slots[i].StartTime, slots[i].EndTime, slots[j].StartTime, slots[j].EndTime))
                {
                    return $"Các khung giờ trong {ClassroomLifecycleHelper.FormatDayVn(slots[i].DayOfWeek)} đang bị trùng hoặc giao nhau.";
                }
            }
        }
        return null;
    }

    public static async Task<string?> FindClassroomScheduleConflictAsync(
        ApplicationDbContext db,
        int tutorId,
        int? excludeClassroomId,
        DateOnly startDate,
        DateOnly? endDate,
        IReadOnlyList<ScheduleSlotCandidate> candidateSlots,
        IReadOnlyCollection<int> enrolledStudentIds,
        IReadOnlyList<(DateTime ScheduledAtUtc, double DurationHours)> plannedSessionsUtc,
        CancellationToken cancellationToken = default)
    {
        var selfConflict = FindSelfOverlap(candidateSlots);
        if (selfConflict != null) return selfConflict;

        var otherTutorClassrooms = await db.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId
                        && c.Status == ClassroomStatus.Active
                        && (!excludeClassroomId.HasValue || c.Id != excludeClassroomId.Value))
            .Select(c => new
            {
                c.NameClass,
                c.StartDate,
                c.EndDate,
                WeeklySchedules = c.WeeklySchedules.Select(ws => new
                {
                    ws.DayOfWeek,
                    ws.StartTime,
                    ws.EndTime
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var other in otherTutorClassrooms)
        {
            if (!DateRangesOverlap(other.StartDate, other.EndDate, startDate, endDate))
                continue;

            foreach (var cand in candidateSlots)
            {
                var overlapWs = other.WeeklySchedules.FirstOrDefault(ws =>
                    ws.DayOfWeek == cand.DayOfWeek
                    && TimesOverlap(ws.StartTime, ws.EndTime, cand.StartTime, cand.EndTime));
                if (overlapWs != null)
                {
                    return $"Khung giờ {ClassroomLifecycleHelper.FormatDayVn(cand.DayOfWeek)} ({cand.StartTime:HH\\:mm}–{cand.EndTime:HH\\:mm}) bị trùng với lớp \"{other.NameClass}\" ({overlapWs.StartTime:HH\\:mm}–{overlapWs.EndTime:HH\\:mm}).";
                }
            }
        }

        var minPlannedUtc = plannedSessionsUtc.Count > 0
            ? plannedSessionsUtc.Min(p => p.ScheduledAtUtc).AddHours(-6)
            : ClassroomLifecycleHelper.VietnamDateTimeToUtc(startDate, new TimeOnly(0, 0)).AddHours(-6);

        if (plannedSessionsUtc.Count > 0)
        {
            var tutorSessions = await db.ClassSessions
                .AsNoTracking()
                .Where(s => s.Classroom.TutorId == tutorId
                            && s.Classroom.Status == ClassroomStatus.Active
                            && s.Status != SessionStatus.Cancelled
                            && s.ScheduledAt >= minPlannedUtc
                            && (!excludeClassroomId.HasValue
                                || s.ClassroomId != excludeClassroomId.Value
                                || s.IsMakeup
                                || s.Status == SessionStatus.InProgress
                                || s.Status == SessionStatus.Done))
                .Select(s => new
                {
                    s.ClassroomId,
                    ClassName = s.Classroom.NameClass,
                    s.ScheduledAt,
                    s.DurationHours,
                    s.Classroom.HoursPerSession
                })
                .ToListAsync(cancellationToken);

            foreach (var planned in plannedSessionsUtc)
            {
                foreach (var existing in tutorSessions)
                {
                    var existingHours = existing.DurationHours.HasValue && existing.DurationHours.Value > 0
                        ? ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.DurationHours.Value)
                        : ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.HoursPerSession);

                    if (ClassroomLifecycleHelper.IntervalsOverlap(planned.ScheduledAtUtc, planned.DurationHours, existing.ScheduledAt, existingHours))
                    {
                        var vn = ClassroomLifecycleHelper.ToVietnamTime(planned.ScheduledAtUtc);
                        return $"Buổi học lúc {vn:HH:mm dd/MM/yyyy} bị trùng với lịch của lớp \"{existing.ClassName}\".";
                    }
                }
            }
        }

        if (enrolledStudentIds.Count > 0)
        {
            var studentConflict = await FindStudentScheduleConflictAsync(
                db,
                excludeClassroomId,
                startDate,
                endDate,
                candidateSlots,
                enrolledStudentIds,
                plannedSessionsUtc,
                cancellationToken);
            if (studentConflict != null)
                return studentConflict;
        }

        return null;
    }

    public static async Task<string?> FindStudentScheduleConflictAsync(
        ApplicationDbContext db,
        int? excludeClassroomId,
        DateOnly startDate,
        DateOnly? endDate,
        IReadOnlyList<ScheduleSlotCandidate> candidateSlots,
        IReadOnlyCollection<int> studentIds,
        IReadOnlyList<(DateTime ScheduledAtUtc, double DurationHours)> plannedSessionsUtc,
        CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0 || candidateSlots.Count == 0)
            return null;

        var minPlannedUtc = plannedSessionsUtc.Count > 0
            ? plannedSessionsUtc.Min(p => p.ScheduledAtUtc).AddHours(-6)
            : ClassroomLifecycleHelper.VietnamDateTimeToUtc(startDate, new TimeOnly(0, 0)).AddHours(-6);

        var studentOtherClassrooms = await db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => studentIds.Contains(cs.StudentId)
                         && cs.Classroom.Status == ClassroomStatus.Active
                         && (!excludeClassroomId.HasValue || cs.ClassroomId != excludeClassroomId.Value))
            .Select(cs => new
            {
                StudentName = cs.Student.User.FullName,
                cs.Classroom.NameClass,
                cs.Classroom.StartDate,
                cs.Classroom.EndDate,
                WeeklySchedules = cs.Classroom.WeeklySchedules.Select(ws => new
                {
                    ws.DayOfWeek,
                    ws.StartTime,
                    ws.EndTime
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var sc in studentOtherClassrooms)
        {
            if (!DateRangesOverlap(sc.StartDate, sc.EndDate, startDate, endDate))
                continue;

            foreach (var cand in candidateSlots)
            {
                var overlapWs = sc.WeeklySchedules.FirstOrDefault(ws =>
                    ws.DayOfWeek == cand.DayOfWeek
                    && TimesOverlap(ws.StartTime, ws.EndTime, cand.StartTime, cand.EndTime));
                if (overlapWs != null)
                {
                    return $"Khung giờ {ClassroomLifecycleHelper.FormatDayVn(cand.DayOfWeek)} ({cand.StartTime:HH\\:mm}–{cand.EndTime:HH\\:mm}) bị trùng với lịch học lớp \"{sc.NameClass}\" của học viên {sc.StudentName}.";
                }
            }
        }

        if (plannedSessionsUtc.Count > 0)
        {
            var studentSessions = await db.ClassSessions
                .AsNoTracking()
                .Where(s => s.Classroom.Status == ClassroomStatus.Active
                            && s.Status != SessionStatus.Cancelled
                            && (!excludeClassroomId.HasValue || s.ClassroomId != excludeClassroomId.Value)
                            && s.ScheduledAt >= minPlannedUtc
                            && s.Classroom.ClassroomStudents.Any(cs => studentIds.Contains(cs.StudentId)))
                .Select(s => new
                {
                    ClassName = s.Classroom.NameClass,
                    s.ScheduledAt,
                    s.DurationHours,
                    s.Classroom.HoursPerSession
                })
                .ToListAsync(cancellationToken);

            foreach (var planned in plannedSessionsUtc)
            {
                foreach (var existing in studentSessions)
                {
                    var existingHours = existing.DurationHours.HasValue && existing.DurationHours.Value > 0
                        ? ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.DurationHours.Value)
                        : ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.HoursPerSession);

                    if (ClassroomLifecycleHelper.IntervalsOverlap(planned.ScheduledAtUtc, planned.DurationHours, existing.ScheduledAt, existingHours))
                    {
                        var vn = ClassroomLifecycleHelper.ToVietnamTime(planned.ScheduledAtUtc);
                        return $"Buổi học lúc {vn:HH:mm dd/MM/yyyy} bị trùng với lịch học lớp \"{existing.ClassName}\" của học viên đang theo học.";
                    }
                }
            }
        }

        return null;
    }

    public static async Task<string?> FindSingleSessionConflictAsync(
        ApplicationDbContext db,
        int tutorId,
        int? excludeClassroomId,
        int? excludeSessionId,
        DateTime startUtc,
        double durationHours,
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var safeDuration = ClassroomLifecycleHelper.NormalizeHoursPerSession(durationHours);
        var utcStart = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        var windowFrom = utcStart.AddHours(-6);
        var windowTo = utcStart.AddHours(safeDuration + 6);

        var tutorNearbySessions = await db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.TutorId == tutorId
                        && s.Classroom.Status == ClassroomStatus.Active
                        && s.Status != SessionStatus.Cancelled
                        && (!excludeSessionId.HasValue || s.Id != excludeSessionId.Value)
                        && s.ScheduledAt >= windowFrom
                        && s.ScheduledAt <= windowTo)
            .Select(s => new
            {
                ClassName = s.Classroom.NameClass,
                s.ScheduledAt,
                s.DurationHours,
                s.Classroom.HoursPerSession
            })
            .ToListAsync(cancellationToken);

        foreach (var existing in tutorNearbySessions)
        {
            var existingHours = existing.DurationHours.HasValue && existing.DurationHours.Value > 0
                ? ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.DurationHours.Value)
                : ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.HoursPerSession);

            if (ClassroomLifecycleHelper.IntervalsOverlap(utcStart, safeDuration, existing.ScheduledAt, existingHours))
                return $"Gia sư đang có buổi học trùng giờ trong lớp \"{existing.ClassName}\".";
        }

        var vnStartDt = ClassroomLifecycleHelper.ToVietnamTime(utcStart);
        var vnDate = DateOnly.FromDateTime(vnStartDt);
        var vnStartTime = TimeOnly.FromDateTime(vnStartDt);
        var vnEndTime = vnStartTime.AddHours(safeDuration);

        var tutorWeekly = await db.ClassroomWeeklySchedules
            .AsNoTracking()
            .Where(ws => ws.Classroom.TutorId == tutorId
                         && ws.Classroom.Status == ClassroomStatus.Active
                         && (!excludeClassroomId.HasValue || ws.ClassroomId != excludeClassroomId.Value)
                         && ws.DayOfWeek == vnDate.DayOfWeek)
            .Select(ws => new
            {
                ws.Classroom.NameClass,
                ws.Classroom.StartDate,
                ws.Classroom.EndDate,
                ws.StartTime,
                ws.EndTime
            })
            .ToListAsync(cancellationToken);

        foreach (var ws in tutorWeekly)
        {
            if (DateRangesOverlap(ws.StartDate, ws.EndDate, vnDate, vnDate)
                && TimesOverlap(ws.StartTime, ws.EndTime, vnStartTime, vnEndTime))
            {
                return $"Khung giờ bị trùng với lịch cố định ({ws.StartTime:HH\\:mm}–{ws.EndTime:HH\\:mm}) của lớp \"{ws.NameClass}\".";
            }
        }

        if (studentIds.Count > 0)
        {
            var studentNearbySessions = await db.ClassSessions
                .AsNoTracking()
                .Where(s => s.Classroom.Status == ClassroomStatus.Active
                            && s.Status != SessionStatus.Cancelled
                            && (!excludeSessionId.HasValue || s.Id != excludeSessionId.Value)
                            && s.ScheduledAt >= windowFrom
                            && s.ScheduledAt <= windowTo
                            && s.Classroom.ClassroomStudents.Any(cs => studentIds.Contains(cs.StudentId)))
                .Select(s => new
                {
                    ClassName = s.Classroom.NameClass,
                    s.ScheduledAt,
                    s.DurationHours,
                    s.Classroom.HoursPerSession
                })
                .ToListAsync(cancellationToken);

            foreach (var existing in studentNearbySessions)
            {
                var existingHours = existing.DurationHours.HasValue && existing.DurationHours.Value > 0
                    ? ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.DurationHours.Value)
                    : ClassroomLifecycleHelper.NormalizeHoursPerSession(existing.HoursPerSession);

                if (ClassroomLifecycleHelper.IntervalsOverlap(utcStart, safeDuration, existing.ScheduledAt, existingHours))
                    return $"Học viên trong lớp đang có buổi học trùng giờ ở lớp \"{existing.ClassName}\".";
            }

            var studentWeekly = await db.ClassroomWeeklySchedules
                .AsNoTracking()
                .Where(ws => ws.Classroom.Status == ClassroomStatus.Active
                             && (!excludeClassroomId.HasValue || ws.ClassroomId != excludeClassroomId.Value)
                             && ws.DayOfWeek == vnDate.DayOfWeek
                             && ws.Classroom.ClassroomStudents.Any(cs => studentIds.Contains(cs.StudentId)))
                .Select(ws => new
                {
                    ws.Classroom.NameClass,
                    ws.Classroom.StartDate,
                    ws.Classroom.EndDate,
                    ws.StartTime,
                    ws.EndTime
                })
                .ToListAsync(cancellationToken);

            foreach (var ws in studentWeekly)
            {
                if (DateRangesOverlap(ws.StartDate, ws.EndDate, vnDate, vnDate)
                    && TimesOverlap(ws.StartTime, ws.EndTime, vnStartTime, vnEndTime))
                {
                    return $"Khung giờ bị trùng với lịch cố định của học viên ở lớp \"{ws.NameClass}\".";
                }
            }
        }

        return null;
    }
}
