using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class CaseEvent : SoftDeletableEntity
{
    public int Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public string Content { get; set; } = "";

    [MaxLength(50)]
    public string Category { get; set; } = "comment";

    [MaxLength(50)]
    public string? Source { get; set; }

    [MaxLength(200)]
    public string? SourceGroupId { get; set; }

    [MaxLength(200)]
    public string? SourceSenderId { get; set; }

    public ICollection<CaseEventCase> Cases { get; set; } = new List<CaseEventCase>();
    public ICollection<CaseEventAttachment> Attachments { get; set; } = new List<CaseEventAttachment>();
    public MeetingEventLink? MeetingLink { get; set; }
}
