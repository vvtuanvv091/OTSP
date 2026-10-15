namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Khung giờ gia sư có thể dạy trong tuần (lặp lại hàng tuần).
/// </summary>
public class TutorAvailabilityDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public string DayOfWeekName => DayOfWeek switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => DayOfWeek.ToString()
    };

    public string TimeRange => $"{StartTime:HH\\:mm} - {EndTime:HH\\:mm}";
}
