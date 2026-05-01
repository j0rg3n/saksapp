using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;
using SaksAppWeb.Services;

namespace SaksAppWeb.Controllers;

[Authorize]
public class CasesController : Controller
{
    public const long MaxUploadBytes = 10 * 1024 * 1024; // 10 MB for now

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly ICaseNumberAllocator _caseNumberAllocator;
    private readonly ICaseQueryService _caseQuery;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        ICaseNumberAllocator caseNumberAllocator,
        ICaseQueryService caseQuery,
        ILogger<CasesController> logger)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _caseNumberAllocator = caseNumberAllocator;
        _caseQuery = caseQuery;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CaseStatus? status, string? assigneeUserId, bool showClosed, CancellationToken ct)
    {
        var vm = await _caseQuery.GetFilteredCasesAsync(status, assigneeUserId, showClosed, ct);
        return View(vm);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var vm = await _caseQuery.GetCaseDetailsAsync(id, ct);
        if (vm is null) return NotFound();
        return View(vm);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new CaseEditVm();
        await PopulateAssignees(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CaseEditVm vm, CancellationToken ct)
    {
        await PopulateAssignees(vm, ct);

        if (!ModelState.IsValid)
            return View(vm);

        var nextCaseNumber = await _caseNumberAllocator.AllocateNextAsync(ct);

        var entity = new BoardCase
        {
            CaseNumber = nextCaseNumber,
            Title = vm.Title,
            Description = vm.Description,
            Theme = vm.Theme,
            Priority = vm.Priority,
            AssigneeUserId = vm.AssigneeUserId,
            StartDate = vm.StartDate,
            Status = vm.Status,
            ClosedDate = vm.ClosedDate,
            CustomTidsfristDate = vm.CustomTidsfristDate,
            CustomTidsfristText = vm.CustomTidsfristText
        };

        _db.BoardCases.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            entityType: nameof(BoardCase),
            entityId: entity.Id.ToString(),
            before: null,
            after: new
            {
                entity.Id,
                entity.CaseNumber,
                entity.Title,
                entity.Priority,
                entity.AssigneeUserId,
                entity.Status
            },
            ct: ct);

return RedirectToAction(nameof(Details), new { id = entity.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entity = await _db.BoardCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        var vm = new CaseEditVm
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Theme = entity.Theme,
            Priority = entity.Priority,
            AssigneeUserId = entity.AssigneeUserId,
            StartDate = entity.StartDate,
            Status = entity.Status,
            ClosedDate = entity.ClosedDate,
            CustomTidsfristDate = entity.CustomTidsfristDate,
            CustomTidsfristText = entity.CustomTidsfristText
        };

        await PopulateAssignees(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CaseEditVm vm, CancellationToken ct)
    {
        if (id != vm.Id) return BadRequest();

        await PopulateAssignees(vm, ct);

        if (!ModelState.IsValid)
            return View(vm);

        var entity = await _db.BoardCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        var before = new
        {
            entity.Title,
            entity.Description,
            entity.Theme,
            entity.Priority,
            entity.AssigneeUserId,
            entity.StartDate,
            entity.Status,
            entity.ClosedDate,
            entity.CustomTidsfristDate,
            entity.CustomTidsfristText
        };

        var oldStatus = entity.Status;

        entity.Title = vm.Title;
        entity.Description = vm.Description;
        entity.Theme = vm.Theme;
        entity.Priority = vm.Priority;
        entity.AssigneeUserId = vm.AssigneeUserId;
        entity.StartDate = vm.StartDate;
        entity.Status = vm.Status;
        entity.ClosedDate = vm.ClosedDate;
        entity.CustomTidsfristDate = vm.CustomTidsfristDate;
        entity.CustomTidsfristText = vm.CustomTidsfristText;

        await _db.SaveChangesAsync(ct);

        var after = new
        {
            entity.Title,
            entity.Description,
            entity.Theme,
            entity.Priority,
            entity.AssigneeUserId,
            entity.StartDate,
            entity.Status,
            entity.ClosedDate,
            entity.CustomTidsfristDate,
            entity.CustomTidsfristText
        };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(BoardCase),
            entity.Id.ToString(),
            before,
            after,
            ct: ct);

        if (oldStatus != entity.Status)
        {
            await _audit.LogAsync(
                AuditAction.StateChange,
                nameof(BoardCase),
                entity.Id.ToString(),
                before: new { Status = oldStatus },
                after: new { Status = entity.Status },
                ct: ct);
        }

        return RedirectToAction(nameof(Details), new { id = entity.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken ct)
    {
        var entity = await _db.BoardCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        var before = new { entity.IsDeleted, entity.DeletedAt, entity.DeletedByUserId, entity.CaseNumber, entity.Title };

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await _db.SaveChangesAsync(ct);

        var after = new { entity.IsDeleted, entity.DeletedAt, entity.DeletedByUserId, entity.CaseNumber, entity.Title };

        await _audit.LogAsync(AuditAction.SoftDelete, nameof(BoardCase), entity.Id.ToString(), before, after, ct: ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int caseId, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return RedirectToAction(nameof(Details), new { id = caseId });

        var c = await _db.BoardCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == caseId, ct);
        if (c is null) return NotFound();

        var entity = new CaseComment
        {
            BoardCaseId = caseId,
            Text = text.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        };

        _db.CaseComments.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(CaseComment),
            entity.Id.ToString(),
            before: null,
            after: new { entity.Id, entity.BoardCaseId, entity.CreatedAt, entity.CreatedByUserId, entity.Text },
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = caseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCommentAttachment(int commentId, IFormFile file, CancellationToken ct)
    {
        _logger.LogInformation("UploadCommentAttachment called: commentId={CommentId}, file={File}", commentId, file?.FileName);
        
        var comment = await _db.CaseComments.FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (comment is null) return NotFound();

        if (file is null || file.Length <= 0)
        {
            _logger.LogWarning("UploadCommentAttachment: file is null or empty");
            return RedirectToAction(nameof(Details), new { id = comment.BoardCaseId });
        }

        if (file.Length > MaxUploadBytes)
            return BadRequest($"File too large. Max is {MaxUploadBytes} bytes.");

        var contentType = file.ContentType ?? "application/octet-stream";
        _logger.LogInformation("ContentType: {ContentType}, Length: {Length}", contentType, file.Length);
        
        if (!IsAllowedContentType(contentType))
            return BadRequest("Only PDF and common image types are allowed.");

        _logger.LogInformation("Reading file content...");
        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }
        _logger.LogInformation("File content read, size: {Size}", bytes.Length);

        var attachment = new Attachment
        {
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            Content = bytes,
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        };

        _db.Attachments.Add(attachment);
        _logger.LogInformation("Saving attachment to DB...");
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Attachment saved with ID: {Id}", attachment.Id);

        var link = new CaseCommentAttachment
        {
            CaseCommentId = commentId,
            AttachmentId = attachment.Id
        };

        _db.CaseCommentAttachments.Add(link);
        _logger.LogInformation("Saving link to DB...");
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Link saved with ID: {Id}", link.Id);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(Attachment),
            attachment.Id.ToString(),
            before: null,
            after: new { attachment.Id, attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAt },
            ct: ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(CaseCommentAttachment),
            link.Id.ToString(),
            before: null,
            after: new { link.Id, link.CaseCommentId, link.AttachmentId },
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = comment.BoardCaseId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id, CancellationToken ct)
    {
        var a = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();

        return File(a.Content, a.ContentType, a.OriginalFileName);
    }

    private async Task PopulateAssignees(CaseEditVm vm, CancellationToken ct)
    {
        var users = await _userManager.Users
            .OrderBy(x => x.FullName ?? x.Email)
            .ToListAsync(ct);

        vm.Assignees = users
            .Select(u => new SelectListItem(u.FullName ?? u.Email ?? u.UserName ?? u.Id, u.Id))
            .ToList();
    }

    public static bool IsAllowedContentType(string contentType)
    {
        return contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
               || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCommentAttachment(int linkId, CancellationToken ct)
    {
        var link = await _db.CaseCommentAttachments
            .Include(x => x.CaseComment)
            .FirstOrDefaultAsync(x => x.Id == linkId, ct);

        if (link is null) return NotFound();

        var before = new { link.Id, link.CaseCommentId, link.AttachmentId, link.IsDeleted, link.DeletedAt, link.DeletedByUserId };

        link.IsDeleted = true;
        link.DeletedAt = DateTimeOffset.UtcNow;
        link.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await _db.SaveChangesAsync(ct);

        var after = new { link.Id, link.CaseCommentId, link.AttachmentId, link.IsDeleted, link.DeletedAt, link.DeletedByUserId };

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(CaseCommentAttachment),
            link.Id.ToString(),
            before,
            after,
            reason: "Unlinked attachment from comment",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = link.CaseComment.BoardCaseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDeleteComment(int commentId, CancellationToken ct)
    {
        var comment = await _db.CaseComments.FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (comment is null) return NotFound();

        var before = new { comment.Id, comment.BoardCaseId, comment.Text, comment.IsDeleted, comment.DeletedAt, comment.DeletedByUserId };

        comment.IsDeleted = true;
        comment.DeletedAt = DateTimeOffset.UtcNow;
        comment.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await _db.SaveChangesAsync(ct);

        var after = new { comment.Id, comment.BoardCaseId, comment.Text, comment.IsDeleted, comment.DeletedAt, comment.DeletedByUserId };

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(CaseComment),
            comment.Id.ToString(),
            before,
            after,
            reason: "Soft deleted comment",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = comment.BoardCaseId });
    }

    public async Task<IActionResult> EditComment(int commentId, CancellationToken ct)
    {
        var comment = await _db.CaseComments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (comment is null) return NotFound();

        var vm = new SaksAppWeb.Models.ViewModels.CommentEditVm
        {
            Id = comment.Id,
            CaseId = comment.BoardCaseId,
            Text = comment.Text
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(SaksAppWeb.Models.ViewModels.CommentEditVm vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var comment = await _db.CaseComments.FirstOrDefaultAsync(x => x.Id == vm.Id, ct);
        if (comment is null) return NotFound();

        var before = new { comment.Id, comment.BoardCaseId, comment.Text };

        comment.Text = vm.Text;

        await _db.SaveChangesAsync(ct);

        var after = new { comment.Id, comment.BoardCaseId, comment.Text };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(CaseComment),
            comment.Id.ToString(),
            before,
            after,
            reason: "Edited comment",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = comment.BoardCaseId });
    }
}
