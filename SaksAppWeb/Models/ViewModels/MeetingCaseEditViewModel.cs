using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models.ViewModels;

public class MeetingCaseEditVm
{
    public int Id { get; set; }
    public int MeetingId { get; set; }

    public int CaseNumber { get; set; }
    public string CaseTitle { get; set; } = "";

    [Required]
    public string AgendaTextSnapshot { get; set; } = "";

    public DateOnly? TidsfristOverrideDate { get; set; }

    [MaxLength(300)]
    public string? TidsfristOverrideText { get; set; }

    public string? FollowUpTextDraft { get; set; }
}
