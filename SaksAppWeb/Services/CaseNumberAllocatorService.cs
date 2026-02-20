using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;

namespace SaksAppWeb.Services;

public sealed class CaseNumberAllocator : ICaseNumberAllocator
{
    private readonly ApplicationDbContext _db;

    public CaseNumberAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> AllocateNextAsync(CancellationToken ct = default)
    {
        // SQLite concurrency note:
        // BEGIN IMMEDIATE acquires a reserved lock early, preventing two writers
        // from both picking the same "max + 1".
        await _db.Database.ExecuteSqlRawAsync("BEGIN IMMEDIATE;", ct);

        try
        {
            var max = await _db.BoardCases
                .IgnoreQueryFilters()
                .MaxAsync(x => (int?)x.CaseNumber, ct);

            var next = (max ?? 0) + 1;

            // Commit the immediate transaction. EF will open its own transaction for SaveChanges later.
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
