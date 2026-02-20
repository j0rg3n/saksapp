namespace SaksAppWeb.Models;

public class MeetingMinutesAttachment : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public int AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
