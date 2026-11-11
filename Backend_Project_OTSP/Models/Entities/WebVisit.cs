using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models;

/// <summary>
/// Ghi nhận lượt truy cập web theo từng thiết bị mỗi ngày (1 thiết bị / ngày = 1 lượt).
/// Tách riêng khỏi AuditLogs để không lẫn nhật ký thao tác hệ thống.
/// </summary>
public class WebVisit
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Ngày truy cập theo giờ Việt Nam (UTC+7).</summary>
    public DateOnly VisitDate { get; set; }

    public DateTime FirstVisitedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastVisitedAt { get; set; } = DateTime.UtcNow;

    public int HitCount { get; set; } = 1;

    [MaxLength(200)]
    public string? FirstPath { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [ForeignKey("User")]
    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }
}

