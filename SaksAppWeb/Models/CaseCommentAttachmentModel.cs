namespace SaksAppWeb.Models;

public class CaseCommentAttachment : SoftDeletableEntity
{
    public int Id { get; set; }

    public int CaseCommentId { get; set; }
    public CaseComment CaseComment { get; set; } = null!;

    public int AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
