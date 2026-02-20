using Microsoft.AspNetCore.Mvc.Rendering;

namespace SaksAppWeb.Models.ViewModels;

public class MeetingDetailsVm
{
    public Meeting Meeting { get; set; } = null!;

    public IReadOnlyList<MeetingAgendaRowVm> Agenda { get; set; } = Array.Empty<MeetingAgendaRowVm>();

    public List<SelectListItem> OpenCasesToAdd { get; set; } = new();

    public int? SelectedCaseIdToAdd { get; set; }
}

public class MeetingAgendaRowVm
{
    public int MeetingCaseId { get; set; }
    public int AgendaOrder { get; set; }

    public int CaseId { get; set; }
    public int CaseNumber { get; set; }
    public string Title { get; set; } = "";

    public string AssigneeDisplay { get; set; } = "";

    public string AgendaTextSnapshot { get; set; } = "";

    public DateOnly? TidsfristOverrideDate { get; set; }
    public string? TidsfristOverrideText { get; set; }
}
