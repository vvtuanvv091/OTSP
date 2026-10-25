using PROJECT_OTSP.Dtos;

namespace PROJECT_OTSP.Services;

public interface IAvailabilitySlotService
{
    Task<List<TutorAvailableSlotDto>> GetAvailableSlotsAsync(
        int tutorId,
        int daysAhead = 14,
        CancellationToken cancellationToken = default);
}
