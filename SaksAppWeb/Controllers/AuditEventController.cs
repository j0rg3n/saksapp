using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;

namespace SaksAppWeb.Controllers;

[Authorize]
public class AuditController : Controller
{
    private readonly ApplicationDbContext _db;

    public AuditController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, int take = 200, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 50, 1000);

        var query = _db.AuditEvents
            .AsNoTracking()
            .OrderByDescending(x => x.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = (IOrderedQueryable<SaksAppWeb.Models.AuditEvent>)query.Where(x =>
                x.EntityType.Contains(q) ||
                x.EntityId.Contains(q) ||
                (x.Reason != null && x.Reason.Contains(q)));
        }

        var items = await query.Take(take).ToListAsync(ct);

        ViewBag.Query = q;
        ViewBag.Take = take;

        return View(items);
    }
}
