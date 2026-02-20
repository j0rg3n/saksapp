using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public interface IPdfSequenceService
{
    Task<int> AllocateNextAsync(int meetingId, PdfDocumentType documentType, CancellationToken ct = default);
}
