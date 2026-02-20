using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models.ViewModels;

public class CommentEditVm
{
    public int Id { get; set; }

    public int CaseId { get; set; }

    [Required]
    public string Text { get; set; } = "";
}
