using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models.ViewModels;

public class CaseEventIndexVm
{
    public IReadOnlyList<CaseEventRowVm> Events { get; set; } = new List<CaseEventRowVm>();
    public string? CategoryFilter { get; set; }
}

public class CaseEventRowVm
{
    public int Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Category { get; set; } = "";
    public string Content { get; set; } = "";
    public IReadOnlyList<int> LinkedCaseNumbers { get; set; } = new List<int>();
    public string? AuthorDisplay { get; set; }
}

public class CaseEventCreateVm
{
    [Required]
    public string Category { get; set; } = "avvik";

    [Required]
    public string Content { get; set; } = "";

    public string? CaseNumbers { get; set; }
}

public class CaseEventEditVm
{
    public int Id { get; set; }

    [Required]
    public string Category { get; set; } = "";

    [Required]
    public string Content { get; set; } = "";

    public string? CaseNumbers { get; set; }
}
