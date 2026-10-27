namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Đánh giá và nhận xét của học viên đối với gia sư sau các buổi học.
/// </summary>
public class TutorReviewDto
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentAvatarUrl { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime ReviewedAt { get; set; }

    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - ReviewedAt;
            if (span.TotalDays > 30) return ReviewedAt.ToString("dd/MM/yyyy");
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays} ngày trước";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours} giờ trước";
            return "Vừa xong";
        }
    }
}

/// <summary>
/// Dữ liệu hiển thị trang đánh giá từ học viên cho gia sư.
/// </summary>
public class TutorReviewPageVm
{
    public double AverageRating { get; set; } = 4.8;
    public int TotalReviews { get; set; } = 98;
    public List<TutorReviewDto> Reviews { get; set; } = new();
}
