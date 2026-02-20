namespace SaksAppWeb.Models;

public class MeetingMinutesCaseEntry : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public int MeetingCaseId { get; set; }
    public MeetingCase MeetingCase { get; set; } = null!;

    public int BoardCaseId { get; set; }
    public BoardCase BoardCase { get; set; } = null!;

    public string? OfficialNotes { get; set; }

    public string? DecisionText { get; set; }

    public string? FollowUpText { get; set; }

    public MeetingCaseOutcome Outcome { get; set; } = MeetingCaseOutcome.Continue;
}
