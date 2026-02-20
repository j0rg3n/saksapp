using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public abstract class SoftDeletableEntity
{
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    [MaxLength(450)]
    public string? DeletedByUserId { get; set; }
}
