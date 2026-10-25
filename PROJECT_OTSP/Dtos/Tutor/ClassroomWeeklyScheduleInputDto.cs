namespace PROJECT_OTSP.Dtos;

public sealed class ClassroomWeeklyScheduleInputDto
{
    public int SessionIndex { get; set; } = 1;
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;
    public TimeOnly StartTime { get; set; } = new TimeOnly(19, 0);
    public TimeOnly EndTime { get; set; } = new TimeOnly(21, 0);

    public double DurationHours => (EndTime - StartTime).TotalHours > 0 
        ? Math.Round((EndTime - StartTime).TotalHours, 1) 
        : 2.0;

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
}
