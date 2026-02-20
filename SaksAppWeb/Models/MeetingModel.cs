using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class Meeting : SoftDeletableEntity
{
    public int Id { get; set; }

    public DateOnly MeetingDate { get; set; }

    public int Year { get; set; }

    public int YearSequenceNumber { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }
}
