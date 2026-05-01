using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;
using SaksAppWeb.Services;

namespace SaksAppWeb.Controllers;

[Authorize]
public class CaseEventsController : Controller
{
    private static readonly string[] EditableCategories = ["avvik", "tiltak", "general"];

    private static readonly Dictionary<string, string> CategoryLabels = new()
    {
        ["avvik"] = "Avvik",
        ["tiltak"] = "Tiltak",
        ["general"] = "Generelt"
    };

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IUserDisplayService _userDisplay;

    public CaseEventsController(
        ApplicationDbContext db,
        IAuditService audit,
        IUserDisplayService userDisplay)
    {
        _db = db;
        _audit = audit;
        _userDisplay = userDisplay;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? category, CancellationToken ct)
    {
        var q = _db.CaseEvents.AsNoTracking()
            .Include(x => x.Cases).ThenInclude(x => x.BoardCase)
            .Where(x => EditableCategories.Contains(x.Category));

        if (!string.IsNullOrWhiteSpace(category) && EditableCategories.Contains(category))
            q = q.Where(x => x.Category == category);

        var events = (await q.ToListAsync(ct))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var authorIds = events
            .Select(x => x.CreatedByUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList()!;

        var displayById = await _userDisplay.GetDisplayNamesAsync(authorIds, ct);

        var rows = events.Select(e => new CaseEventRowVm
        {
            Id = e.Id,
            CreatedAt = e.CreatedAt,
            Category = e.Category,
            Content = e.Content,
            LinkedCaseNumbers = e.Cases
                .Where(x => !x.IsDeleted)
                .Select(x => x.BoardCase.CaseNumber)
                .OrderBy(x => x)
                .ToList(),
            AuthorDisplay = e.CreatedByUserId is not null && displayById.TryGetValue(e.CreatedByUserId, out var d) ? d : e.CreatedByUserId
        }).ToList();

        return View(new CaseEventIndexVm { Events = rows, CategoryFilter = category });
    }

    [HttpGet]
    public IActionResult Create(string? category)
    {
        return View(new CaseEventCreateVm { Category = category ?? "avvik" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CaseEventCreateVm vm, CancellationToken ct)
    {
        if (!EditableCategories.Contains(vm.Category))
            ModelState.AddModelError(nameof(vm.Category), "Ugyldig kategori.");

        if (!ModelState.IsValid)
            return View(vm);

        var entity = new CaseEvent
        {
            Category = vm.Category,
            Content = vm.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        _db.CaseEvents.Add(entity);
        await _db.SaveChangesAsync(ct);

        var linkedCaseNumbers = ParseCaseNumbers(vm.CaseNumbers);
        if (linkedCaseNumbers.Count > 0)
        {
            var cases = await _db.BoardCases
                .Where(x => linkedCaseNumbers.Contains(x.CaseNumber))
                .ToListAsync(ct);

            foreach (var c in cases)
                _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = entity.Id, BoardCaseId = c.Id });

            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(CaseEvent),
            entity.Id.ToString(),
            before: null,
            after: new { entity.Id, entity.Category, entity.Content, entity.CreatedAt },
            ct: ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var ev = await _db.CaseEvents.AsNoTracking()
            .Include(x => x.Cases).ThenInclude(x => x.BoardCase)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (ev is null) return NotFound();

        var caseNumbers = ev.Cases
            .Where(x => !x.IsDeleted)
            .Select(x => x.BoardCase.CaseNumber.ToString())
            .ToList();

        return View(new CaseEventEditVm
        {
            Id = ev.Id,
            Category = ev.Category,
            Content = ev.Content,
            CaseNumbers = string.Join(", ", caseNumbers)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CaseEventEditVm vm, CancellationToken ct)
    {
        if (!EditableCategories.Contains(vm.Category))
            ModelState.AddModelError(nameof(vm.Category), "Ugyldig kategori.");

        if (!ModelState.IsValid)
            return View(vm);

        var entity = await _db.CaseEvents
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == vm.Id, ct);

        if (entity is null) return NotFound();

        var before = new { entity.Category, entity.Content };

        entity.Category = vm.Category;
        entity.Content = vm.Content.Trim();

        // Replace case links
        var existingLinks = entity.Cases.Where(x => !x.IsDeleted).ToList();
        foreach (var link in existingLinks)
        {
            link.IsDeleted = true;
            link.DeletedAt = DateTimeOffset.UtcNow;
            link.DeletedByUserId = _audit.GetActorUserId();
        }

        var newCaseNumbers = ParseCaseNumbers(vm.CaseNumbers);
        if (newCaseNumbers.Count > 0)
        {
            var cases = await _db.BoardCases
                .Where(x => newCaseNumbers.Contains(x.CaseNumber))
                .ToListAsync(ct);

            foreach (var c in cases)
                _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = entity.Id, BoardCaseId = c.Id });
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(CaseEvent),
            entity.Id.ToString(),
            before,
            after: new { entity.Category, entity.Content },
            ct: ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken ct)
    {
        var entity = await _db.CaseEvents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        var before = new { entity.Id, entity.IsDeleted, entity.DeletedAt, entity.DeletedByUserId };

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeletedByUserId = _audit.GetActorUserId();

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(CaseEvent),
            entity.Id.ToString(),
            before,
            after: new { entity.Id, entity.IsDeleted, entity.DeletedAt, entity.DeletedByUserId },
            ct: ct);

        return RedirectToAction(nameof(Index));
    }

    public static List<int> ParseCaseNumbers(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<int>();

        return input
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();
    }

    public static string CategoryLabel(string category) =>
        CategoryLabels.TryGetValue(category, out var label) ? label : category;
}
