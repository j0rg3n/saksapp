using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class MeetingEventLink : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public int CaseEventId { get; set; }
    public CaseEvent CaseEvent { get; set; } = null!;

    public int AgendaOrder { get; set; }

    public string AgendaTextSnapshot { get; set; } = "";

    public DateOnly? TidsfristOverrideDate { get; set; }

    [MaxLength(300)]
    public string? TidsfristOverrideText { get; set; }

    public string? OfficialNotes { get; set; }

    public string? DecisionText { get; set; }

    public string? FollowUpText { get; set; }

    public MeetingCaseOutcome? Outcome { get; set; }

    public bool IsEventuelt { get; set; }
}
