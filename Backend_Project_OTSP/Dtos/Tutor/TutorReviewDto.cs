namespace Backend_Project_OTSP.Dtos.Tutor;

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
/// Dữ liệu hiển thị trang đánh giá từ học viên cho gia sư (kèm tỷ lệ phần trăm các mức sao và phân trang).
/// </summary>
public class TutorReviewPageVm
{
    public double AverageRating { get; set; } = 4.9;
    public int TotalReviews { get; set; } = 48;
    public List<TutorReviewDto> Reviews { get; set; } = new();

    // Thống kê tỷ lệ đánh giá theo số sao (5 sao -> 1 sao)
    public int Rating5Percent { get; set; } = 90;
    public int Rating4Percent { get; set; } = 8;
    public int Rating3Percent { get; set; } = 2;
    public int Rating2Percent { get; set; } = 0;
    public int Rating1Percent { get; set; } = 0;

    public int Rating5Count { get; set; } = 43;
    public int Rating4Count { get; set; } = 4;
    public int Rating3Count { get; set; } = 1;
    public int Rating2Count { get; set; } = 0;
    public int Rating1Count { get; set; } = 0;

    // Hỗ trợ phân trang dữ liệu
    public PaginationVm? Pagination { get; set; }
}
