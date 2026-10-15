using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers
{
    [Area("Tutor")]
    [Authorize(Roles = "Tutor")]
    public class TutorAvailabilityController : TutorAreaController
    {
        private const int FirstHour = 6;
        private const int LastHour = 22;
        private const int VietnamUtcOffsetHours = 7;

        private readonly ApplicationDbContext _dbContext;

        public TutorAvailabilityController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
            if (tutorId is null)
                return NotFound();

            var availabilities = await _dbContext.TutorAvailabilities
                .AsNoTracking()
                .Where(x => x.TutorId == tutorId.Value)
                .OrderBy(x => x.DayOfWeek)
                .ThenBy(x => x.StartTime)
                .Select(x => new TutorAvailabilityDto
                {
                    DayOfWeek = x.DayOfWeek,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime
                })
                .ToListAsync(cancellationToken);

            ViewBag.BusySlotKeys = await GetBusySlotKeysAsync(tutorId.Value, cancellationToken);
            return RenderIndex(availabilities);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            [FromForm(Name = "SelectedSlotKeys")] string[] selectedSlotKeys,
            CancellationToken cancellationToken)
        {
            var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
            if (tutorId is null)
                return NotFound();

            // Recheck booked hours on the server; disabled cells in the page are not trusted.
            var busySlotKeys = await GetBusySlotKeysAsync(tutorId.Value, cancellationToken);
            var selectedSlots = (selectedSlotKeys ?? [])
                .Select(ParseSlotKey)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Where(x => !busySlotKeys.Contains(ToSlotKey(x.Day, x.Hour)))
                .Distinct()
                .ToList();

            // Xóa trực tiếp trên DB, không tải danh sách cũ vào bộ nhớ ChangeTracker
            await _dbContext.TutorAvailabilities
                .Where(x => x.TutorId == tutorId.Value)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var slot in selectedSlots)
            {
                _dbContext.TutorAvailabilities.Add(new TutorAvailability
                {
                    TutorId = tutorId.Value,
                    DayOfWeek = slot.Day,
                    StartTime = new TimeOnly(slot.Hour, 0),
                    EndTime = new TimeOnly(slot.Hour + 1, 0)
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Đã lưu lịch rảnh hàng tuần.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetCurrentTutorIdAsync(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _dbContext.TutorProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<HashSet<string>> GetBusySlotKeysAsync(
            int tutorId,
            CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var scheduledTimesUtc = await _dbContext.ClassSessions
                .AsNoTracking()
                .Where(x => x.Status == SessionStatus.Scheduled
                            && x.ScheduledAt >= nowUtc
                            && x.Classroom.TutorId == tutorId
                            && x.Classroom.Status == ClassroomStatus.Active)
                .Select(x => x.ScheduledAt)
                .ToListAsync(cancellationToken);

            var busy = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scheduledUtc in scheduledTimesUtc)
            {
                var vietnamTime = DateTime.SpecifyKind(scheduledUtc, DateTimeKind.Utc)
                    .AddHours(VietnamUtcOffsetHours);

                if (vietnamTime.Hour >= FirstHour && vietnamTime.Hour < LastHour)
                    busy.Add(ToSlotKey(vietnamTime.DayOfWeek, vietnamTime.Hour));
            }

            return busy;
        }

        private static (DayOfWeek Day, int Hour)? ParseSlotKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            var parts = key.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var dayValue)
                || !int.TryParse(parts[1], out var hour)
                || !Enum.IsDefined(typeof(DayOfWeek), dayValue)
                || hour < FirstHour
                || hour >= LastHour)
            {
                return null;
            }

            return ((DayOfWeek)dayValue, hour);
        }

        private static string ToSlotKey(DayOfWeek day, int hour) => $"{(int)day}:{hour}";
    }
}
