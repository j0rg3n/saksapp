using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public sealed class PdfSequenceService : IPdfSequenceService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public PdfSequenceService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> AllocateNextAsync(int meetingId, PdfDocumentType documentType, CancellationToken ct = default)
    {
        // Same pattern as CaseNumber allocation: reserve a write lock early.
        await _db.Database.ExecuteSqlRawAsync("BEGIN IMMEDIATE;", ct);

        try
        {
            var max = await _db.PdfGenerations
                .Where(x => x.MeetingId == meetingId && x.DocumentType == documentType)
                .MaxAsync(x => (int?)x.SequenceNumber, ct);

            var next = (max ?? 0) + 1;

            _db.PdfGenerations.Add(new PdfGeneration
            {
                MeetingId = meetingId,
                DocumentType = documentType,
                SequenceNumber = next,
                GeneratedByUserId = _audit.GetActorUserId()
            });

            await _db.SaveChangesAsync(ct);
            await _db.Database.ExecuteSqlRawAsync("COMMIT;", ct);

            return next;
        }
        catch
        {
            try { await _db.Database.ExecuteSqlRawAsync("ROLLBACK;", ct); } catch { /* best effort */ }
            throw;
        }
    }
}
