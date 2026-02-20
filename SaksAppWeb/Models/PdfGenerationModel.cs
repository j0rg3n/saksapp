using System.ComponentModel.DataAnnotations;

namespace SaksAppWeb.Models;

public class PdfGeneration
{
    public long Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public PdfDocumentType DocumentType { get; set; }

    public int SequenceNumber { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string? GeneratedByUserId { get; set; }
}
