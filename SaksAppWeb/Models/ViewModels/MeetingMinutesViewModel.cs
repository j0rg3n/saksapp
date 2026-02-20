using System.ComponentModel.DataAnnotations;
using SaksAppWeb.Models;

namespace SaksAppWeb.Models.ViewModels;

public class MeetingMinutesVm
{
    public int MeetingId { get; set; }

    public DateOnly MeetingDate { get; set; }
    public int Year { get; set; }
    public int YearSequenceNumber { get; set; }
    public string? Location { get; set; }

    public string? AttendanceText { get; set; }
    public string? AbsenceText { get; set; }
    public string? ApprovalOfPreviousMinutesText { get; set; }

    public DateOnly? NextMeetingDate { get; set; }

    public string? EventueltText { get; set; }

    public List<MeetingMinutesCaseEntryVm> CaseEntries { get; set; } = new();

    public List<SignedMinutesVm> SignedMinutes { get; set; } = new();
}

public class MeetingMinutesCaseEntryVm
{
    public int MeetingCaseId { get; set; }
    public int BoardCaseId { get; set; }
    public int CaseNumber { get; set; }
    public string Title { get; set; } = "";
    public string? AssigneeDisplay { get; set; }

    public string? OfficialNotes { get; set; }
    public string? DecisionText { get; set; }
    public string? FollowUpText { get; set; }

    public MeetingCaseOutcome Outcome { get; set; } = MeetingCaseOutcome.Continue;

    public int? MinutesEntryId { get; set; }

    public List<MinutesEntryAttachmentVm> Attachments { get; set; } = new();
}

public class MinutesEntryAttachmentVm
{
    public int LinkId { get; set; }
    public int AttachmentId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
}

public class SignedMinutesVm
{
    public int AttachmentId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public long SizeBytes { get; set; }
}
