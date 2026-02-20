using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class CaseComment : SoftDeletableEntity
{
    public int Id { get; set; }

    public int BoardCaseId { get; set; }

    public BoardCase BoardCase { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = "";

    [Required]
    public string Text { get; set; } = "";
}
