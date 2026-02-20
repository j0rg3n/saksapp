using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models.ViewModels;

public class MeetingEditVm
{
    public int? Id { get; set; }

    public DateOnly MeetingDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public int YearSequenceNumber { get; set; } = 1;

    public string? Location { get; set; }
}
