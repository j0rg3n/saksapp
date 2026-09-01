using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class Meeting : SoftDeletableEntity
{
    public int Id { get; set; }

    public DateOnly MeetingDate { get; set; }

    /// <summary>
    /// Start time of the meeting. Nullable because meetings created before this field existed
    /// have no time recorded; consumers must fall back to end-of-day rather than guessing.
    /// </summary>
    public TimeOnly? MeetingTime { get; set; }

    public int Year { get; set; }

    public int YearSequenceNumber { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }
}
