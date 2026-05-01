namespace SaksAppWeb.Models;

public class CaseEventAttachment : SoftDeletableEntity
{
    public int Id { get; set; }

    public int CaseEventId { get; set; }
    public CaseEvent CaseEvent { get; set; } = null!;

    public int AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
