using SaksAppWeb.Models;

namespace SaksAppWeb.Models.ViewModels;

public class CaseIndexRowVm
{
    public int Id { get; set; }
    public int CaseNumber { get; set; }
    public string Title { get; set; } = "";
    public CasePriority Priority { get; set; }
    public CaseStatus Status { get; set; }

    public string AssigneeUserId { get; set; } = "";
    public string AssigneeDisplay { get; set; } = "";

    public DateOnly? CustomTidsfristDate { get; set; }
    public string? CustomTidsfristText { get; set; }
}
