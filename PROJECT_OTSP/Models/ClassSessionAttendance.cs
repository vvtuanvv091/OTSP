using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models;

public sealed class ClassSessionAttendance
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(ClassSession))]
    public int ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAtUtc { get; set; }
    public int JoinCount { get; set; } = 1;
}
