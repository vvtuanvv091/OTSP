<<<<<<< Updated upstream
=======
﻿using System.ComponentModel.DataAnnotations;
>>>>>>> Stashed changes
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models;

// Yêu cầu kết nối từ học viên/phụ huynh gửi tới gia sư
public class ConnectionRequest
{
    [Key]
    public int Id { get; set; }

    [MaxLength(1000)]
    public string? Message { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    // Thời điểm học viên gửi yêu cầu (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Thời điểm gia sư phản hồi (UTC), null khi còn Pending
    public DateTime? RespondedAt { get; set; }

    // Khung giờ học thử đề xuất do học viên chọn từ lịch rảnh của gia sư (UTC)
    public DateTime? ProposedTrialSlotUtc { get; set; }

    [ForeignKey("Student")]
    public int StudentId { get; set; }
    public StudentProfile Student { get; set; } = null!;

    [ForeignKey("Tutor")]
    public int TutorId { get; set; }
    public TutorProfile Tutor { get; set; } = null!;

<<<<<<< Updated upstream
=======
    // Môn-cấp học học viên muốn học
    [ForeignKey("SubjectLevel")]
    public int SubjectLevelId { get; set; }
    public SubjectLevel SubjectLevel { get; set; } = null!;

>>>>>>> Stashed changes
    // Kiểu học học viên muốn: 1-1 hoặc nhóm
    public ClassroomType PreferredType { get; set; } = ClassroomType.OneToOne;

    // Chỉ có khi xin vào một lớp nhóm đã mở
    [ForeignKey("Classroom")]
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }

    public ICollection<TrialSchedule> TrialSchedules { get; set; } = new List<TrialSchedule>();
<<<<<<< Updated upstream

=======
>>>>>>> Stashed changes
    // Môn học (ví dụ: Toán)
    [ForeignKey("Subject")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    // Cấp học (ví dụ: THCS)
    [ForeignKey("GradeLevel")]
    public int GradeLevelId { get; set; }
    public GradeLevel GradeLevel { get; set; } = null!;
<<<<<<< Updated upstream
=======

>>>>>>> Stashed changes
}