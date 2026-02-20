namespace SaksAppWeb.Models;

public enum AuditAction
{
    Create = 1,
    Update = 2,
    SoftDelete = 3,
    StateChange = 4,
    GeneratePdf = 5,
    UploadSignedMinutes = 6
}
