using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class AuditEvent
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [Required, MaxLength(100)]
    public string EntityType { get; set; } = "";

    public string EntityId { get; set; } = "";

    public AuditAction Action { get; set; }

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}
