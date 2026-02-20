using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SaksAppWeb.Models.ViewModels;

public class CaseEditVm
{
    public int? Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Theme { get; set; }

    public CasePriority Priority { get; set; } = CasePriority.P2;

    [Required]
    public string AssigneeUserId { get; set; } = "";

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public CaseStatus Status { get; set; } = CaseStatus.Open;

    public DateOnly? ClosedDate { get; set; }

    public DateOnly? CustomTidsfristDate { get; set; }

    [MaxLength(300)]
    public string? CustomTidsfristText { get; set; }

    public List<SelectListItem> Assignees { get; set; } = new();
}
