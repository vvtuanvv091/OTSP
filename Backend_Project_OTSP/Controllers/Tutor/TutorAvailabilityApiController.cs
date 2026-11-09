using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/availabilities")]
[Authorize(Roles = "Tutor")]
public class TutorAvailabilityApiController : ControllerBase
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;
    private readonly IScheduleBlockService _blocks;

    public TutorAvailabilityApiController(ApplicationDbContext db, IScheduleBlockService blocks)
    {
        _db = db;
        _blocks = blocks;
    }

    private async Task<int?> GetCurrentTutorIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return null;

        return await _db.TutorProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailabilities(CancellationToken cancellationToken)
    {
        var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var availabilities = await _db.TutorAvailabilities
            .AsNoTracking()
            .Where(x => x.TutorId == tutorId.Value)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                DayOfWeek = (int)x.DayOfWeek,
                DayOfWeekName = x.DayOfWeek.ToString(),
                StartTime = $"{x.StartTime:HH\\:mm}",
                EndTime = $"{x.EndTime:HH\\:mm}"
            })
            .ToListAsync(cancellationToken);

        var busySlotKeys = await GetBusySlotKeysAsync(tutorId.Value, cancellationToken);

        return Ok(new
        {
            availabilities,
            busySlotKeys
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveAvailabilities([FromBody] SaveAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var busySlotKeys = await GetBusySlotKeysAsync(tutorId.Value, cancellationToken);
        var selectedSlots = (request.SelectedSlotKeys ?? Array.Empty<string>())
            .Select(ParseSlotKey)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Where(x => !busySlotKeys.Contains(ToSlotKey(x.Day, x.Block.Start)))
            .Distinct()
            .ToList();

        var existing = await _db.TutorAvailabilities
            .Where(x => x.TutorId == tutorId.Value)
            .ToListAsync(cancellationToken);

        _db.TutorAvailabilities.RemoveRange(existing);

        foreach (var slot in selectedSlots)
        {
            _db.TutorAvailabilities.Add(new TutorAvailability
            {
                TutorId = tutorId.Value,
                DayOfWeek = slot.Day,
                StartTime = new TimeOnly(slot.Block.Start, 0),
                EndTime = new TimeOnly(slot.Block.End, 0)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã lưu lịch rảnh hàng tuần thành công." });
    }

    private async Task<HashSet<string>> GetBusySlotKeysAsync(int tutorId, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var scheduledTimesUtc = await _db.ClassSessions
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

            var block = _blocks.Containing(vietnamTime.Hour);
            if (block.HasValue)
                busy.Add(ToSlotKey(vietnamTime.DayOfWeek, block.Value.Start));
        }

        return busy;
    }

    private (DayOfWeek Day, TimeBlock Block)? ParseSlotKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        var parts = key.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var dayValue)
            || !int.TryParse(parts[1], out var hour)
            || !Enum.IsDefined(typeof(DayOfWeek), dayValue))
        {
            return null;
        }

        var block = _blocks.FindByStart(hour);
        if (!block.HasValue) return null;

        return ((DayOfWeek)dayValue, block.Value);
    }

    private static string ToSlotKey(DayOfWeek day, int startHour) => $"{(int)day}:{startHour}";
}

public class SaveAvailabilityRequest
{
    public string[]? SelectedSlotKeys { get; set; }
}

