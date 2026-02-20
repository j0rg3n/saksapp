using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class Attachment : SoftDeletableEntity
{
    public int Id { get; set; }

    [Required, MaxLength(260)]
    public string OriginalFileName { get; set; } = "";

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = "";

    public long SizeBytes { get; set; }

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string UploadedByUserId { get; set; } = "";
}
