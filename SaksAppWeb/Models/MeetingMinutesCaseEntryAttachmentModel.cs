namespace SaksAppWeb.Models;

public class MeetingMinutesCaseEntryAttachment : SoftDeletableEntity
{
    public int Id { get; set; }

    public int MeetingMinutesCaseEntryId { get; set; }
    public MeetingMinutesCaseEntry MeetingMinutesCaseEntry { get; set; } = null!;

    public int AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
