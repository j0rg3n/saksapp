namespace SaksAppWeb.Models;

public class MeetingCaseAttachment : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingCaseId { get; set; }
    public MeetingCase MeetingCase { get; set; } = null!;

    public int AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
