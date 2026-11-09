using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;

namespace Backend_Project_OTSP.Services.Tutor;

public interface IAvailabilitySlotService
{
    Task<List<TutorAvailableSlotDto>> GetAvailableSlotsAsync(
        int tutorId,
        int daysAhead = 14,
        CancellationToken cancellationToken = default);

    Task<List<TutorAvailableSlotDto>> GetAvailableSlotsForStudentAsync(
        int tutorId,
        int studentId,
        int daysAhead = 14,
        CancellationToken cancellationToken = default);
}
