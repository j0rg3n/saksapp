using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class MeetingCase : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public int BoardCaseId { get; set; }
    public BoardCase BoardCase { get; set; } = null!;

    public int AgendaOrder { get; set; }

    public int? AgendaPointNumber { get; set; }

    [Required]
    public string AgendaTextSnapshot { get; set; } = "";

    public DateOnly? TidsfristOverrideDate { get; set; }

    [MaxLength(300)]
    public string? TidsfristOverrideText { get; set; }

    public MeetingCaseOutcome? Outcome { get; set; }

    public string? FollowUpTextDraft { get; set; }
}
