namespace SaksAppWeb.Models.ViewModels;

public class CaseDetailsVm
{
    public BoardCase Case { get; set; } = null!;
    public IReadOnlyDictionary<string, string> UserDisplayById { get; set; }
        = new Dictionary<string, string>();

    public string? AssigneeDisplay { get; set; }

    public IReadOnlyList<CaseTimelineItemVm> Timeline { get; set; } = Array.Empty<CaseTimelineItemVm>();
}

public enum CaseTimelineItemKind
{
    Comment = 1,
    Minutes = 2
}

public enum CaseAttachmentLinkKind
{
    CommentAttachment = 1,
    MinutesEntryAttachment = 2
}

public class CaseAttachmentVm
{
    public CaseAttachmentLinkKind LinkKind { get; set; }

    public int AttachmentId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }

    public int? LinkId { get; set; }
}

public class CaseTimelineItemVm
{
    public CaseTimelineItemKind Kind { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public long SortId { get; set; }

    // Comment-specific
    public int? CommentId { get; set; }
    public string? CommentText { get; set; }
    public string? CommentAuthorUserId { get; set; }

    // Minutes-specific
    public int? MeetingMinutesCaseEntryId { get; set; }
    public int? MeetingId { get; set; }

    public DateOnly? MeetingDate { get; set; }
    public int? MeetingYear { get; set; }
    public int? MeetingYearSequenceNumber { get; set; }

    public MeetingCaseOutcome? Outcome { get; set; }
    public string? OfficialNotes { get; set; }
    public string? DecisionText { get; set; }
    public string? FollowUpText { get; set; }

    public List<CaseAttachmentVm> Attachments { get; set; } = new();
}
