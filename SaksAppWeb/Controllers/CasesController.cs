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

    public CasesController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        ICaseNumberAllocator caseNumberAllocator)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _caseNumberAllocator = caseNumberAllocator;
    }

    public async Task<IActionResult> Index(CaseStatus? status, string? assigneeUserId, CancellationToken ct)
    {
        var q = _db.BoardCases.AsNoTracking();

        if (status is not null)
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(assigneeUserId))
            q = q.Where(x => x.AssigneeUserId == assigneeUserId);

        var cases = await q
            .OrderByDescending(x => x.CaseNumber)
            .ToListAsync(ct);

        var assigneeIds = cases
            .Select(x => x.AssigneeUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var users = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToListAsync(ct);

        var displayById = users.ToDictionary(
            x => x.Id,
            x => x.FullName ?? x.Email ?? x.UserName ?? x.Id);

        var vm = cases.Select(c => new SaksAppWeb.Models.ViewModels.CaseIndexRowVm
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                Title = c.Title,
                Priority = c.Priority,
                Status = c.Status,
                AssigneeUserId = c.AssigneeUserId,
                AssigneeDisplay = displayById.TryGetValue(c.AssigneeUserId, out var d) ? d : c.AssigneeUserId,
                CustomTidsfristDate = c.CustomTidsfristDate,
                CustomTidsfristText = c.CustomTidsfristText
            })
            .ToList();

        return View(vm);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var c = await _db.BoardCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

    // 1) Unofficial comments (no SQL ordering due to DateTimeOffset limitation; we sort in memory)
    var comments = await _db.CaseComments.AsNoTracking()
        .Where(x => x.BoardCaseId == id)
        .ToListAsync(ct);

    // minutesRows query: include MeetingCaseId so we can fetch agenda attachments
var minutesRows = await _db.MeetingMinutesCaseEntries.AsNoTracking()
    .Where(x => x.BoardCaseId == id)
    .Join(_db.Meetings.AsNoTracking(),
        e => e.MeetingId,
        m => m.Id,
        (e, m) => new
        {
            MinutesEntryId = e.Id,
            e.MeetingId,
            m.MeetingDate,
            m.Year,
            m.YearSequenceNumber,
            e.Outcome,
            e.OfficialNotes,
            e.DecisionText,
            e.FollowUpText
        })
    .ToListAsync(ct);

// Minutes entry attachments (by MeetingMinutesCaseEntryId)
var minutesEntryIds = minutesRows.Select(x => x.MinutesEntryId).Distinct().ToList();

var minutesAtts = minutesEntryIds.Count == 0
    ? new List<(int EntryId, SaksAppWeb.Models.ViewModels.CaseAttachmentVm Att)>()
    : await _db.MeetingMinutesCaseEntryAttachments.AsNoTracking()
        .Where(x => minutesEntryIds.Contains(x.MeetingMinutesCaseEntryId))
        .Join(_db.Attachments.AsNoTracking(),
            link => link.AttachmentId,
            att => att.Id,
            (link, att) => new
            {
                link.MeetingMinutesCaseEntryId,
                LinkId = link.Id,
                AttachmentId = att.Id,
                att.OriginalFileName,
                att.ContentType,
                att.SizeBytes
            })
        .OrderByDescending(x => x.AttachmentId)
        .Select(x => new ValueTuple<int, SaksAppWeb.Models.ViewModels.CaseAttachmentVm>(
            x.MeetingMinutesCaseEntryId,
            new SaksAppWeb.Models.ViewModels.CaseAttachmentVm
            {
                LinkKind = SaksAppWeb.Models.ViewModels.CaseAttachmentLinkKind.MinutesEntryAttachment,
                LinkId = x.LinkId,
                AttachmentId = x.AttachmentId,
                OriginalFileName = x.OriginalFileName,
                ContentType = x.ContentType,
                SizeBytes = x.SizeBytes
            }))
        .ToListAsync(ct);

var minutesAttByEntryId = minutesAtts
    .GroupBy(x => x.Item1)
    .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToList());

// comment attachments - keep your existing code

    // Collect user IDs to show friendly display names
    var userIds = comments
        .Select(x => x.CreatedByUserId)
        .Append(c.AssigneeUserId)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct()
        .ToList();

    var users = await _userManager.Users
        .Where(u => userIds.Contains(u.Id))
        .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
        .ToListAsync(ct);

    var userDisplayById = users.ToDictionary(
        x => x.Id,
        x => x.FullName ?? x.Email ?? x.UserName ?? x.Id);

    // Build timeline items
    var timeline = new List<SaksAppWeb.Models.ViewModels.CaseTimelineItemVm>();

    foreach (var com in comments)
    {
        timeline.Add(new SaksAppWeb.Models.ViewModels.CaseTimelineItemVm
        {
            Kind = SaksAppWeb.Models.ViewModels.CaseTimelineItemKind.Comment,
            OccurredAt = com.CreatedAt,
            SortId = com.Id,
            CommentId = com.Id,
            CommentText = com.Text,
            CommentAuthorUserId = com.CreatedByUserId
        });
    }

foreach (var mr in minutesRows)
{
    // Use meeting date for chronology. Put it at noon UTC to avoid “midnight” confusion in display.
    var occurredAt = new DateTimeOffset(
        year: mr.MeetingDate.Year,
        month: mr.MeetingDate.Month,
        day: mr.MeetingDate.Day,
        hour: 12, minute: 0, second: 0,
        offset: TimeSpan.Zero);

    timeline.Add(new SaksAppWeb.Models.ViewModels.CaseTimelineItemVm
    {
        Kind = SaksAppWeb.Models.ViewModels.CaseTimelineItemKind.Minutes,
        OccurredAt = occurredAt,
        SortId = mr.MinutesEntryId,
        MeetingMinutesCaseEntryId = mr.MinutesEntryId,
        MeetingId = mr.MeetingId,
        MeetingDate = mr.MeetingDate,
        MeetingYear = mr.Year,
        MeetingYearSequenceNumber = mr.YearSequenceNumber,
        Outcome = mr.Outcome,
        OfficialNotes = mr.OfficialNotes,
        DecisionText = mr.DecisionText,
        FollowUpText = mr.FollowUpText
    });
}

foreach (var item in timeline)
{
    if (item.Kind == SaksAppWeb.Models.ViewModels.CaseTimelineItemKind.Minutes)
    {
        if (item.MeetingMinutesCaseEntryId is int meid && minutesAttByEntryId.TryGetValue(meid, out var minutesList))
            item.Attachments.AddRange(minutesList);
    }

    // comments: keep as before
}

    // Reverse chronological:
    // - primarily by OccurredAt
    // - tie-breaker by Kind (Minutes after Comments on same day, or vice versa—pick one)
    // - tie-breaker by SortId
    var ordered = timeline
        .OrderByDescending(x => x.OccurredAt)
        .ThenByDescending(x => x.Kind)   // Minutes (2) before Comment (1) on same timestamp
        .ThenByDescending(x => x.SortId)
        .ToList();

    var vm = new SaksAppWeb.Models.ViewModels.CaseDetailsVm
    {
        Case = c,
        UserDisplayById = userDisplayById,
        AssigneeDisplay = userDisplayById.TryGetValue(c.AssigneeUserId, out var d) ? d : c.AssigneeUserId,
        Timeline = ordered
    };

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
        var comment = await _db.CaseComments.FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (comment is null) return NotFound();

        if (file is null || file.Length <= 0)
            return RedirectToAction(nameof(Details), new { id = comment.BoardCaseId });

        if (file.Length > MaxUploadBytes)
            return BadRequest($"File too large. Max is {MaxUploadBytes} bytes.");

        var contentType = file.ContentType ?? "application/octet-stream";
        if (!IsAllowedContentType(contentType))
            return BadRequest("Only PDF and common image types are allowed.");

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

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
        await _db.SaveChangesAsync(ct);

        var link = new CaseCommentAttachment
        {
            CaseCommentId = commentId,
            AttachmentId = attachment.Id
        };

        _db.CaseCommentAttachments.Add(link);
        await _db.SaveChangesAsync(ct);

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
