namespace PROJECT_OTSP.Dtos;

public class TutorAvailableSlotDto
{
    public DateTime SlotUtc { get; set; }
    public string IsoValue => SlotUtc.ToString("o");
    public string DateLabel { get; set; } = string.Empty;
    public string TimeRange { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string DisplayText => $"{DateLabel} ({TimeRange})";
}
