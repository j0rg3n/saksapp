namespace SaksAppWeb.Models;

public class MeetingMinutes : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public string? AttendanceText { get; set; }
    public string? AbsenceText { get; set; }
    public string? ApprovalOfPreviousMinutesText { get; set; }

    public DateOnly? NextMeetingDate { get; set; }

    public string? EventueltText { get; set; }
}
