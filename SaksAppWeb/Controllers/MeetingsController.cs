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
public class MeetingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPdfSequenceService _pdfSequence;

    public MeetingsController(ApplicationDbContext db, IAuditService audit, UserManager<ApplicationUser> userManager, IPdfSequenceService pdfSequence)
    {
        _db = db;
        _audit = audit;
        _userManager = userManager;
        _pdfSequence = pdfSequence;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var meetings = await _db.Meetings.AsNoTracking()
            .OrderByDescending(x => x.MeetingDate)
            .ToListAsync(ct);

        return View(meetings);
    }

    public IActionResult Create()
    {
        return View(new MeetingEditVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MeetingEditVm vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var meeting = new Meeting
        {
            MeetingDate = vm.MeetingDate,
            Year = vm.MeetingDate.Year,
            YearSequenceNumber = vm.YearSequenceNumber,
            Location = vm.Location
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(Meeting),
            meeting.Id.ToString(),
            before: null,
            after: new { meeting.Id, meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber, meeting.Location },
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = meeting.Id });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (meeting is null) return NotFound();

        var agendaEntities = await _db.MeetingCases.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Join(_db.BoardCases.AsNoTracking(),
                mc => mc.BoardCaseId,
                c => c.Id,
                (mc, c) => new { mc, c })
            .OrderBy(x => x.mc.AgendaOrder)
            .ToListAsync(ct);

        var assigneeIds = agendaEntities
            .Select(x => x.c.AssigneeUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var agenda = agendaEntities.Select(x => new MeetingAgendaRowVm
            {
                MeetingCaseId = x.mc.Id,
                AgendaOrder = x.mc.AgendaOrder,
                CaseId = x.c.Id,
                CaseNumber = x.c.CaseNumber,
                Title = x.c.Title,
                AssigneeDisplay = userDisplay.TryGetValue(x.c.AssigneeUserId, out var d) ? d : x.c.AssigneeUserId,
                AgendaTextSnapshot = x.mc.AgendaTextSnapshot,
                TidsfristOverrideDate = x.mc.TidsfristOverrideDate,
                TidsfristOverrideText = x.mc.TidsfristOverrideText
            })
            .ToList();

        var alreadyScheduledCaseIds = agendaEntities.Select(x => x.c.Id).Distinct().ToList();

        var openCases = await _db.BoardCases.AsNoTracking()
            .Where(x => x.Status != CaseStatus.Closed)
            .Where(x => !alreadyScheduledCaseIds.Contains(x.Id))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CaseNumber)
            .Select(x => new { x.Id, x.CaseNumber, x.Title })
            .ToListAsync(ct);

        var vm = new MeetingDetailsVm
        {
            Meeting = meeting,
            Agenda = agenda,
            OpenCasesToAdd = openCases
                .Select(c => new SelectListItem($"#{c.CaseNumber} — {c.Title}", c.Id.ToString()))
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCase(int meetingId, int caseId, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null) return NotFound();

        var boardCase = await _db.BoardCases.FirstOrDefaultAsync(x => x.Id == caseId, ct);
        if (boardCase is null) return NotFound();

        var exists = await _db.MeetingCases.AnyAsync(x => x.MeetingId == meetingId && x.BoardCaseId == caseId, ct);
        if (exists) return RedirectToAction(nameof(Details), new { id = meetingId });

        var maxOrder = await _db.MeetingCases
            .Where(x => x.MeetingId == meetingId)
            .MaxAsync(x => (int?)x.AgendaOrder, ct);

        var agendaText = !string.IsNullOrWhiteSpace(boardCase.Description)
            ? boardCase.Description!
            : boardCase.Title;

        var mc = new MeetingCase
        {
            MeetingId = meetingId,
            BoardCaseId = caseId,
            AgendaOrder = (maxOrder ?? 0) + 1,
            AgendaTextSnapshot = agendaText
        };

        _db.MeetingCases.Add(mc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(MeetingCase),
            mc.Id.ToString(),
            before: null,
            after: new
            {
                mc.Id,
                mc.MeetingId,
                mc.BoardCaseId,
                mc.AgendaOrder,
                mc.AgendaTextSnapshot,
                mc.TidsfristOverrideDate,
                mc.TidsfristOverrideText
            },
            reason: "Added case to meeting agenda",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    public async Task<IActionResult> EditAgendaItem(int id, CancellationToken ct)
    {
        var mc = await _db.MeetingCases.AsNoTracking()
            .Join(_db.BoardCases.AsNoTracking(),
                x => x.BoardCaseId,
                c => c.Id,
                (x, c) => new { x, c })
            .FirstOrDefaultAsync(z => z.x.Id == id, ct);

        if (mc is null) return NotFound();

        var vm = new MeetingCaseEditVm
        {
            Id = mc.x.Id,
            MeetingId = mc.x.MeetingId,
            CaseNumber = mc.c.CaseNumber,
            CaseTitle = mc.c.Title,
            AgendaTextSnapshot = mc.x.AgendaTextSnapshot,
            TidsfristOverrideDate = mc.x.TidsfristOverrideDate,
            TidsfristOverrideText = mc.x.TidsfristOverrideText,
            FollowUpTextDraft = mc.x.FollowUpTextDraft
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAgendaItem(MeetingCaseEditVm vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var mc = await _db.MeetingCases.FirstOrDefaultAsync(x => x.Id == vm.Id, ct);
        if (mc is null) return NotFound();

        var before = new
        {
            mc.AgendaTextSnapshot,
            mc.TidsfristOverrideDate,
            mc.TidsfristOverrideText,
            mc.FollowUpTextDraft
        };

        mc.AgendaTextSnapshot = vm.AgendaTextSnapshot;
        mc.TidsfristOverrideDate = vm.TidsfristOverrideDate;
        mc.TidsfristOverrideText = vm.TidsfristOverrideText;
        mc.FollowUpTextDraft = vm.FollowUpTextDraft;

        await _db.SaveChangesAsync(ct);

        var after = new
        {
            mc.AgendaTextSnapshot,
            mc.TidsfristOverrideDate,
            mc.TidsfristOverrideText,
            mc.FollowUpTextDraft
        };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(MeetingCase),
            mc.Id.ToString(),
            before,
            after,
            reason: "Edited agenda item",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = mc.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAgendaItem(int id, CancellationToken ct)
    {
        var mc = await _db.MeetingCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (mc is null) return NotFound();

        var before = new { mc.Id, mc.MeetingId, mc.BoardCaseId, mc.IsDeleted, mc.DeletedAt, mc.DeletedByUserId };

        mc.IsDeleted = true;
        mc.DeletedAt = DateTimeOffset.UtcNow;
        mc.DeletedByUserId = _audit.GetActorUserId();

        await _db.SaveChangesAsync(ct);

        var after = new { mc.Id, mc.MeetingId, mc.BoardCaseId, mc.IsDeleted, mc.DeletedAt, mc.DeletedByUserId };

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(MeetingCase),
            mc.Id.ToString(),
            before,
            after,
            reason: "Removed agenda item (soft delete)",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = mc.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveAgendaItem(int id, bool up, CancellationToken ct)
    {
        var mc = await _db.MeetingCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (mc is null) return NotFound();

        var meetingId = mc.MeetingId;

        var neighbor = await _db.MeetingCases
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var idx = neighbor.FindIndex(x => x.Id == id);
        if (idx < 0) return RedirectToAction(nameof(Details), new { id = meetingId });

        var otherIdx = up ? idx - 1 : idx + 1;
        if (otherIdx < 0 || otherIdx >= neighbor.Count)
            return RedirectToAction(nameof(Details), new { id = meetingId });

        var a = neighbor[idx];
        var b = neighbor[otherIdx];

        var before = new
        {
            A = new { a.Id, a.AgendaOrder },
            B = new { b.Id, b.AgendaOrder }
        };

        (a.AgendaOrder, b.AgendaOrder) = (b.AgendaOrder, a.AgendaOrder);

        await _db.SaveChangesAsync(ct);

        var after = new
        {
            A = new { a.Id, a.AgendaOrder },
            B = new { b.Id, b.AgendaOrder }
        };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(MeetingCase),
            entityId: $"{a.Id},{b.Id}",
            before,
            after,
            reason: "Reordered agenda items",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    [HttpGet]
public async Task<IActionResult> DownloadAgendaPdf(int id, CancellationToken ct)
{
    var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (meeting is null) return NotFound();

    var agenda = await _db.MeetingCases.AsNoTracking()
        .Where(x => x.MeetingId == id)
        .Join(_db.BoardCases.AsNoTracking(),
            mc => mc.BoardCaseId,
            c => c.Id,
            (mc, c) => new { mc, c })
        .OrderBy(x => x.mc.AgendaOrder)
        .ToListAsync(ct);

    var assigneeIds = agenda.Select(x => x.c.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
    var userDisplay = await _userManager.Users
        .Where(u => assigneeIds.Contains(u.Id))
        .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
        .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

    var seq = await _pdfSequence.AllocateNextAsync(id, PdfDocumentType.Agenda, ct);

    // --- Preload "previous meeting follow-up" (last minutes entry before this meeting date) ---
    var caseIds = agenda.Select(x => x.c.Id).Distinct().ToList();

    var previousMinutesCandidates = await _db.MeetingMinutesCaseEntries.AsNoTracking()
        .Where(x => caseIds.Contains(x.BoardCaseId))
        .Join(_db.Meetings.AsNoTracking(),
            e => e.MeetingId,
            m => m.Id,
            (e, m) => new
            {
                e.Id,
                e.BoardCaseId,
                e.MeetingId,
                m.MeetingDate,
                e.OfficialNotes,
                e.DecisionText,
                e.FollowUpText
            })
        .Where(x => x.MeetingDate < meeting.MeetingDate)
        .ToListAsync(ct);

    var previousByCaseId = previousMinutesCandidates
        .GroupBy(x => x.BoardCaseId)
        .ToDictionary(
            g => g.Key,
            g => g.OrderByDescending(x => x.MeetingDate).ThenByDescending(x => x.Id).First());

    // --- Preload attachments for those previous minutes entries ---
    var prevEntryIds = previousByCaseId.Values.Select(x => x.Id).Distinct().ToList();

    var prevMinutesAttachmentsFull = prevEntryIds.Count == 0
        ? new List<(int EntryId, string FileName, string ContentType, byte[] Content)>()
        : await _db.MeetingMinutesCaseEntryAttachments.AsNoTracking()
            .Where(x => prevEntryIds.Contains(x.MeetingMinutesCaseEntryId))
            .Join(_db.Attachments.AsNoTracking(),
                link => link.AttachmentId,
                att => att.Id,
                (link, att) => new { link.MeetingMinutesCaseEntryId, att.OriginalFileName, att.ContentType, att.Content })
            .Select(x => new ValueTuple<int, string, string, byte[]>(x.MeetingMinutesCaseEntryId, x.OriginalFileName, x.ContentType, x.Content))
            .ToListAsync(ct);

    var prevMinutesAttachmentsByEntryId = prevMinutesAttachmentsFull
        .GroupBy(x => x.Item1)
        .ToDictionary(g => g.Key, g => g.Select(x => (x.Item2, x.Item3, x.Item4)).Distinct().ToList());
    
    // --- Preload all comments for these cases (filter in memory by time window) ---
    // (SQLite + DateTimeOffset ORDER BY is annoying; we avoid ordering in SQL and sort in memory)
    var allComments = await _db.CaseComments.AsNoTracking()
        .Where(x => caseIds.Contains(x.BoardCaseId))
        .ToListAsync(ct);

    // Preload comment attachments (with content)
    var commentIds = allComments.Select(x => x.Id).Distinct().ToList();

    var commentAttachmentsFull = commentIds.Count == 0
        ? new List<(int CommentId, string FileName, string ContentType, byte[] Content)>()
        : await _db.CaseCommentAttachments.AsNoTracking()
            .Where(x => commentIds.Contains(x.CaseCommentId))
            .Join(_db.Attachments.AsNoTracking(),
                link => link.AttachmentId,
                att => att.Id,
                (link, att) => new { link.CaseCommentId, att.OriginalFileName, att.ContentType, att.Content })
            .Select(x => new ValueTuple<int, string, string, byte[]>(x.CaseCommentId, x.OriginalFileName, x.ContentType, x.Content))
            .ToListAsync(ct);

    var commentAttachmentsByCommentId = commentAttachmentsFull
        .GroupBy(x => x.Item1)
        .ToDictionary(
            g => g.Key,
            g => g.Select(x => (x.Item2, x.Item3, x.Item4)).ToList());

    // --- Build PDF ---
    var pdf = new SimplePdfWriter();
    pdf.Title($"Innkalling styremøte #{meeting.YearSequenceNumber} {meeting.Year} — {meeting.MeetingDate}");
    pdf.Paragraph($"(v{seq})");
    if (!string.IsNullOrWhiteSpace(meeting.Location))
        pdf.Paragraph($"Sted: {meeting.Location}");

    pdf.Blank();
    // TODO: Higher level heading
    pdf.Heading("Agenda");

    // If you want fixed items at top like "Godkjenne forrige referat", you can hardcode them here:
    pdf.Heading2("1. Godkjenne forrige referat.");

    var agendaNumber = 2;

    // We'll also collect a de-duplicated attachment list for the whole agenda
    var referencedAttachments = new Dictionary<string, (string ContentType, byte[] Content)>(StringComparer.OrdinalIgnoreCase);
    
    // Track which attachments belong to which source (prev meeting ID or comment ID)
    var attachmentSources = new Dictionary<string, List<object>>(); // filename -> list of source IDs (prev meeting int.Id or comment int.Id)

    // First pass: collect all attachments and their sources
    foreach (var row in agenda)
    {
        if (previousByCaseId.TryGetValue(row.c.Id, out var prev))
        {
            if (prevMinutesAttachmentsByEntryId.TryGetValue(prev.Id, out var prevAtts) && prevAtts.Count > 0)
            {
                foreach (var att in prevAtts)
                {
                    if (!referencedAttachments.ContainsKey(att.Item1))
                        referencedAttachments[att.Item1] = (att.Item2, att.Item3);
                    
                    if (!attachmentSources.ContainsKey(att.Item1))
                        attachmentSources[att.Item1] = new List<object>();
                    attachmentSources[att.Item1].Add(prev.Id);
                }
            }
        }

        // Comments between meetings
        var windowStart = previousByCaseId.TryGetValue(row.c.Id, out var p2)
            ? new DateTimeOffset(p2.MeetingDate.Year, p2.MeetingDate.Month, p2.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero)
            : DateTimeOffset.MinValue;
        var windowEnd = new DateTimeOffset(meeting.MeetingDate.Year, meeting.MeetingDate.Month, meeting.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero);
        var between = allComments
            .Where(c => c.BoardCaseId == row.c.Id)
            .Where(c => c.CreatedAt > windowStart && c.CreatedAt <= windowEnd)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToList();

        foreach (var com in between)
        {
            if (commentAttachmentsByCommentId.TryGetValue(com.Id, out var atts) && atts.Count > 0)
            {
                foreach (var att in atts)
                {
                    if (!referencedAttachments.ContainsKey(att.Item1))
                        referencedAttachments[att.Item1] = (att.Item2, att.Item3);
                    
                    if (!attachmentSources.ContainsKey(att.Item1))
                        attachmentSources[att.Item1] = new List<object>();
                    attachmentSources[att.Item1].Add(com.Id);
                }
            }
        }
    }

    // Assign numbers to attachments
    var attachmentNumberByFileName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var num = 1;
    foreach (var key in referencedAttachments.Keys)
    {
        attachmentNumberByFileName[key] = num++;
    }

    // Now render the agenda with attachment references
    foreach (var row in agenda)
    {
        var assignee = userDisplay.TryGetValue(row.c.AssigneeUserId, out var d) ? d : row.c.AssigneeUserId;

        // Title line similar to your example
        pdf.Heading2($"{agendaNumber}. {row.c.Title} ({assignee}; #{row.c.CaseNumber})");

        if (!string.IsNullOrWhiteSpace(row.c.Description))
        {
            pdf.ParagraphItalic($"{row.c.Description}");
        }

        // Tidsfrist display (same logic as before)
        var tidsfrist = (row.mc.TidsfristOverrideDate is not null || !string.IsNullOrWhiteSpace(row.mc.TidsfristOverrideText))
            ? $"{row.mc.TidsfristOverrideDate?.ToString() ?? ""} {row.mc.TidsfristOverrideText ?? ""}".Trim()
            : (row.c.CustomTidsfristDate is not null || !string.IsNullOrWhiteSpace(row.c.CustomTidsfristText))
                ? $"{row.c.CustomTidsfristDate?.ToString() ?? ""} {row.c.CustomTidsfristText ?? ""}".Trim()
                : "Innen neste møte";

        // Tidsfrist will be shown in Forrige møte section if there's a previous meeting

        // Previous meeting follow-up (official)
        if (previousByCaseId.TryGetValue(row.c.Id, out var prev))
        {
            pdf.Heading3($"Forrige møte ({prev.MeetingDate}):");

            if (!string.IsNullOrWhiteSpace(prev.OfficialNotes))
            {
                pdf.HeadingInline("Status: ", prev.OfficialNotes);
            }

            if (!string.IsNullOrWhiteSpace(prev.DecisionText))
            {
                pdf.HeadingInline("Vedtak: ", prev.DecisionText);
            }

            if (!string.IsNullOrWhiteSpace(prev.FollowUpText))
            {
                pdf.HeadingInline("Oppfølging: ", prev.FollowUpText);
            }

            if (!string.IsNullOrWhiteSpace(tidsfrist))
            {
                pdf.HeadingInline("Tidsfrist: ", tidsfrist);
            }

            if (prevMinutesAttachmentsByEntryId.TryGetValue(prev.Id, out var prevAtts) && prevAtts.Count > 0)
            {
                var attRefs = string.Join(", ", prevAtts.Select(a => $"[Vedlegg {attachmentNumberByFileName[a.Item1]}]"));
                pdf.WriteTextWithAttachmentLinks($"Vedlegg: {attRefs}");
            }
        }
        else
        {
            // No previous meeting - show Tidsfrist here
            if (!string.IsNullOrWhiteSpace(tidsfrist))
            {
                pdf.HeadingInline("Tidsfrist: ", tidsfrist);
            }
        }

        // Til møtet section - before history
        var tilMotet = !string.IsNullOrWhiteSpace(row.mc.FollowUpTextDraft)
            ? row.mc.FollowUpTextDraft
            : row.mc.AgendaTextSnapshot;

        if (!string.IsNullOrWhiteSpace(tilMotet) &&
            !tilMotet.Equals(row.c.Description, StringComparison.OrdinalIgnoreCase))
        {
            pdf.Heading3("Til møtet:");
            pdf.ParagraphIndented(tilMotet);
        }

        // Comments between meetings
        // window: (prev meeting date, current meeting date]
        var windowStart = previousByCaseId.TryGetValue(row.c.Id, out var p2)
            ? new DateTimeOffset(p2.MeetingDate.Year, p2.MeetingDate.Month, p2.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero)
            : DateTimeOffset.MinValue;

        var windowEnd = new DateTimeOffset(meeting.MeetingDate.Year, meeting.MeetingDate.Month, meeting.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero);

        var between = allComments
            .Where(c => c.BoardCaseId == row.c.Id)
            .Where(c => c.CreatedAt > windowStart && c.CreatedAt <= windowEnd)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToList();

        if (between.Count > 0)
        {
            pdf.Heading3("Historikk siden forrige møte:");

            foreach (var com in between)
            {
                var fullText = $"{com.CreatedAt:yyyy-MM-dd}: {com.Text}";
                
                // Split into first line and continuation at word boundary
                var firstLineMaxLen = 50;
                string firstLine, continuation;
                if (fullText.Length <= firstLineMaxLen)
                {
                    firstLine = fullText;
                    continuation = "";
                }
                else
                {
                    var splitIdx = fullText.LastIndexOf(' ', firstLineMaxLen);
                    if (splitIdx > 10) // only split if we found a reasonable word boundary
                    {
                        firstLine = fullText.Substring(0, splitIdx);
                        continuation = fullText.Substring(splitIdx + 1);
                    }
                    else
                    {
                        firstLine = fullText;
                        continuation = "";
                    }
                }

                pdf.ParagraphFirstLine(firstLine, continuation, isItalic: true);

                if (commentAttachmentsByCommentId.TryGetValue(com.Id, out var atts) && atts.Count > 0)
                {
                    var attRefs = string.Join(", ", atts.Select(a => $"[Vedlegg {attachmentNumberByFileName[a.Item1]}]"));
                    pdf.WriteTextWithAttachmentLinks(attRefs);
                }
            }
        }

        pdf.Blank(10);
        agendaNumber++;
    }

    // Append attachments at the end
    if (referencedAttachments.Count > 0)
    {
        // Attachment list
        pdf.Blank(6);
        pdf.Heading2("Vedlegg");

        var attachmentNumber = 1;
        foreach (var kvp in referencedAttachments)
        {
            var (attachmentFileName, contentType, content) = (kvp.Key, kvp.Value.ContentType, kvp.Value.Content);
            pdf.Paragraph($"Vedlegg {attachmentNumber}: {attachmentFileName}");
            attachmentNumber++;
        }

        // Attachment content
        attachmentNumber = 1;
        foreach (var kvp in referencedAttachments)
        {
            var (attachmentFileName, contentType, content) = (kvp.Key, kvp.Value.ContentType, kvp.Value.Content);
            if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                pdf.AddPdfAttachment(content, attachmentFileName, attachmentNumber);
            }
            else if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                pdf.AddImageAttachment(content, attachmentFileName, attachmentNumber);
            }
            attachmentNumber++;
        }
    }

    var bytes = pdf.ToBytes();

    await _audit.LogAsync(
        AuditAction.GeneratePdf,
        entityType: nameof(Meeting),
        entityId: meeting.Id.ToString(),
        before: null,
        after: new { DocumentType = "Agenda", SequenceNumber = seq, MeetingId = meeting.Id, meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber },
        reason: "Generated enriched agenda PDF (prev follow-up + between-meeting comments + attachment refs)",
        ct: ct);

    var pdfFileName = $"{meeting.Year}-#{meeting.YearSequenceNumber}-innkalling-{meeting.MeetingDate}-v{seq}.pdf";
    return File(bytes, "application/pdf", pdfFileName);
}

    [HttpGet]
    public async Task<IActionResult> DownloadAssigneeReminderPdf(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (meeting is null) return NotFound();

        var agenda = await _db.MeetingCases.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Join(_db.BoardCases.AsNoTracking(),
                mc => mc.BoardCaseId,
                c => c.Id,
                (mc, c) => new { mc, c })
            .OrderBy(x => x.mc.AgendaOrder)
            .ToListAsync(ct);

        var assigneeIds = agenda.Select(x => x.c.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var seq = await _pdfSequence.AllocateNextAsync(id, PdfDocumentType.AssigneeReminder, ct);

        var pdf = new SimplePdfWriter();
        pdf.Title($"Påminnelse per ansvarlig — {meeting.MeetingDate} — {meeting.Year}/{meeting.YearSequenceNumber} — PDF v{seq}");
        pdf.Paragraph("Kort liste per ansvarlig over saker til møtet.");

        pdf.Blank();

        var grouped = agenda
            .GroupBy(x => x.c.AssigneeUserId)
            .OrderBy(g => userDisplay.TryGetValue(g.Key, out var d) ? d : g.Key);

        foreach (var g in grouped)
        {
            var assignee = userDisplay.TryGetValue(g.Key, out var d) ? d : g.Key;
            pdf.Heading(assignee);

            foreach (var row in g.OrderBy(x => x.mc.AgendaOrder))
            {
                var tidsfrist = (row.mc.TidsfristOverrideDate is not null || !string.IsNullOrWhiteSpace(row.mc.TidsfristOverrideText))
                    ? $"{row.mc.TidsfristOverrideDate?.ToString() ?? ""} {row.mc.TidsfristOverrideText ?? ""}".Trim()
                    : (row.c.CustomTidsfristDate is not null || !string.IsNullOrWhiteSpace(row.c.CustomTidsfristText))
                        ? $"{row.c.CustomTidsfristDate?.ToString() ?? ""} {row.c.CustomTidsfristText ?? ""}".Trim()
                        : "Innen neste møte";

                pdf.Paragraph($"- #{row.c.CaseNumber} {row.c.Title} — Tidsfrist: {tidsfrist}");
            }

            pdf.Blank(8);
        }

        var bytes = pdf.ToBytes();

        await _audit.LogAsync(
            AuditAction.GeneratePdf,
            entityType: nameof(Meeting),
            entityId: meeting.Id.ToString(),
            before: null,
            after: new { DocumentType = "AssigneeReminder", SequenceNumber = seq, meeting.Id, meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber },
            reason: "Generated assignee reminder PDF",
            ct: ct);

        var fileName = $"reminder-{meeting.Year}-{meeting.YearSequenceNumber:00}-{meeting.MeetingDate}-v{seq}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    public async Task<IActionResult> Minutes(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (meeting is null) return NotFound();

        var minutes = await _db.MeetingMinutes.FirstOrDefaultAsync(x => x.MeetingId == id, ct);
        if (minutes is null)
        {
            minutes = new MeetingMinutes { MeetingId = id };
            _db.MeetingMinutes.Add(minutes);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(
                AuditAction.Create,
                nameof(MeetingMinutes),
                minutes.Id.ToString(),
                before: null,
                after: new { minutes.Id, minutes.MeetingId },
                reason: "Created minutes record",
                ct: ct);
        }

        var agenda = await _db.MeetingCases.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var agendaCaseIds = agenda.Select(x => x.Id).ToList();

        var existingEntries = await _db.MeetingMinutesCaseEntries
            .Where(x => x.MeetingId == id)
            .ToListAsync(ct);

        var existingByMeetingCaseId = existingEntries.ToDictionary(x => x.MeetingCaseId, x => x);

        // Ensure there is an entry per agenda item
        foreach (var mc in agenda)
        {
            if (existingByMeetingCaseId.ContainsKey(mc.Id))
                continue;

            var entry = new MeetingMinutesCaseEntry
            {
                MeetingId = id,
                MeetingCaseId = mc.Id,
                BoardCaseId = mc.BoardCaseId,
                Outcome = MeetingCaseOutcome.Continue
            };

            _db.MeetingMinutesCaseEntries.Add(entry);
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(
                AuditAction.Update,
                nameof(Meeting),
                meeting.Id.ToString(),
                before: null,
                after: new { AddedMinutesEntries = true },
                reason: "Ensured minutes entries for agenda",
                ct: ct);
        }

        var entries = await _db.MeetingMinutesCaseEntries.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Join(_db.MeetingCases.AsNoTracking(), e => e.MeetingCaseId, mc => mc.Id, (e, mc) => new { e, mc })
            .Join(_db.BoardCases.AsNoTracking(), x => x.e.BoardCaseId, c => c.Id, (x, c) => new { x.e, x.mc, c })
            .OrderBy(x => x.mc.AgendaOrder)
            .ToListAsync(ct);

        var assigneeIds = entries.Select(x => x.c.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var vm = new SaksAppWeb.Models.ViewModels.MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            MeetingDate = meeting.MeetingDate,
            Year = meeting.Year,
            YearSequenceNumber = meeting.YearSequenceNumber,
            Location = meeting.Location,

            AttendanceText = minutes.AttendanceText,
            AbsenceText = minutes.AbsenceText,
            ApprovalOfPreviousMinutesText = minutes.ApprovalOfPreviousMinutesText,
            NextMeetingDate = minutes.NextMeetingDate,
            EventueltText = minutes.EventueltText,

            CaseEntries = entries.Select(x => new SaksAppWeb.Models.ViewModels.MeetingMinutesCaseEntryVm
            {
                MeetingCaseId = x.mc.Id,
                BoardCaseId = x.c.Id,
                CaseNumber = x.c.CaseNumber,
                Title = x.c.Title,
                AssigneeDisplay = userDisplay.TryGetValue(x.c.AssigneeUserId, out var d) ? d : x.c.AssigneeUserId,
                OfficialNotes = x.e.OfficialNotes,
                DecisionText = x.e.DecisionText,
                FollowUpText = x.e.FollowUpText,
                Outcome = x.e.Outcome,
                MinutesEntryId = x.e.Id
            }).ToList()
        };

        var signed = await _db.MeetingMinutesAttachments.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Join(_db.Attachments.AsNoTracking(),
                link => link.AttachmentId,
                att => att.Id,
                (link, att) => new SaksAppWeb.Models.ViewModels.SignedMinutesVm
                {
                    AttachmentId = att.Id,
                    OriginalFileName = att.OriginalFileName,
                    SizeBytes = att.SizeBytes
                })
            .OrderByDescending(x => x.AttachmentId)
            .ToListAsync(ct);

        vm.SignedMinutes = signed;

        var minutesEntryEntities = await _db.MeetingMinutesCaseEntries.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .ToListAsync(ct);

        var minutesEntryIdByMeetingCaseId = minutesEntryEntities
            .ToDictionary(x => x.MeetingCaseId, x => x.Id);

        foreach (var ce in vm.CaseEntries)
        {
            ce.MinutesEntryId = minutesEntryIdByMeetingCaseId.TryGetValue(ce.MeetingCaseId, out var mid) ? mid : null;
        }

        var minutesEntryIds = minutesEntryEntities.Select(x => x.Id).ToList();

        var attachmentRows = minutesEntryIds.Count == 0
            ? new List<(int EntryId, SaksAppWeb.Models.ViewModels.MinutesEntryAttachmentVm Att)>()
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
                .Select(x => new ValueTuple<int, SaksAppWeb.Models.ViewModels.MinutesEntryAttachmentVm>(
                    x.MeetingMinutesCaseEntryId,
                    new SaksAppWeb.Models.ViewModels.MinutesEntryAttachmentVm
                    {
                        LinkId = x.LinkId,
                        AttachmentId = x.AttachmentId,
                        OriginalFileName = x.OriginalFileName,
                        ContentType = x.ContentType,
                        SizeBytes = x.SizeBytes
                    }))
                .ToListAsync(ct);

        var attachmentsByEntryId = attachmentRows
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToList());

        foreach (var ce in vm.CaseEntries)
        {
            if (ce.MinutesEntryId is int eid && attachmentsByEntryId.TryGetValue(eid, out var list))
                ce.Attachments = list;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Minutes(SaksAppWeb.Models.ViewModels.MeetingMinutesVm vm, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == vm.MeetingId, ct);
        if (meeting is null) return NotFound();

        var minutes = await _db.MeetingMinutes.FirstOrDefaultAsync(x => x.MeetingId == vm.MeetingId, ct);
        if (minutes is null) return NotFound();

        var beforeMinutes = new
        {
            minutes.AttendanceText,
            minutes.AbsenceText,
            minutes.ApprovalOfPreviousMinutesText,
            minutes.NextMeetingDate,
            minutes.EventueltText
        };

        minutes.AttendanceText = vm.AttendanceText;
        minutes.AbsenceText = vm.AbsenceText;
        minutes.ApprovalOfPreviousMinutesText = vm.ApprovalOfPreviousMinutesText;
        minutes.NextMeetingDate = vm.NextMeetingDate;
        minutes.EventueltText = vm.EventueltText;

        await _db.SaveChangesAsync(ct);

        var entries = await _db.MeetingMinutesCaseEntries
            .Where(x => x.MeetingId == vm.MeetingId)
            .ToListAsync(ct);

        var entriesByMeetingCaseId = entries.ToDictionary(x => x.MeetingCaseId, x => x);

        foreach (var e in vm.CaseEntries)
        {
            if (!entriesByMeetingCaseId.TryGetValue(e.MeetingCaseId, out var entity))
                continue;

            var beforeEntry = new { entity.OfficialNotes, entity.DecisionText, entity.FollowUpText, entity.Outcome };

            entity.OfficialNotes = e.OfficialNotes;
            entity.DecisionText = e.DecisionText;
            entity.FollowUpText = e.FollowUpText;
            entity.Outcome = e.Outcome;

            var afterEntry = new { entity.OfficialNotes, entity.DecisionText, entity.FollowUpText, entity.Outcome };

            await _audit.LogAsync(
                AuditAction.Update,
                nameof(MeetingMinutesCaseEntry),
                entity.Id.ToString(),
                beforeEntry,
                afterEntry,
                reason: $"Updated minutes entry for case #{e.CaseNumber}",
                ct: ct);
        }

        var afterMinutes = new
        {
            minutes.AttendanceText,
            minutes.AbsenceText,
            minutes.ApprovalOfPreviousMinutesText,
            minutes.NextMeetingDate,
            minutes.EventueltText
        };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(MeetingMinutes),
            minutes.Id.ToString(),
            beforeMinutes,
            afterMinutes,
            reason: "Updated minutes",
            ct: ct);

        await _db.SaveChangesAsync(ct);

        // Optional: update case status based on outcome
        // We'll implement that in Slice 2 together with the PDF, so we can define the exact rule.

        return RedirectToAction(nameof(Minutes), new { id = vm.MeetingId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadMinutesPdf(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (meeting is null) return NotFound();

        var minutes = await _db.MeetingMinutes.AsNoTracking().FirstOrDefaultAsync(x => x.MeetingId == id, ct);
        if (minutes is null) return BadRequest("Minutes record does not exist yet. Open the Minutes page first.");

        var entries = await _db.MeetingMinutesCaseEntries.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Join(_db.MeetingCases.AsNoTracking(), e => e.MeetingCaseId, mc => mc.Id, (e, mc) => new { e, mc })
            .Join(_db.BoardCases.AsNoTracking(), x => x.e.BoardCaseId, c => c.Id, (x, c) => new { x.e, x.mc, c })
            .OrderBy(x => x.mc.AgendaOrder)
            .ToListAsync(ct);

        var assigneeIds = entries.Select(x => x.c.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        // Load attachments for entries
        var entryIds = entries.Select(x => x.e.Id).Distinct().ToList();
        var entryAttachments = entryIds.Count == 0
            ? new List<(int EntryId, string FileName, string ContentType, byte[] Content)>()
            : await _db.MeetingMinutesCaseEntryAttachments.AsNoTracking()
                .Where(x => entryIds.Contains(x.MeetingMinutesCaseEntryId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new { link.MeetingMinutesCaseEntryId, att.OriginalFileName, att.ContentType, att.Content })
                .Select(x => new ValueTuple<int, string, string, byte[]>(x.MeetingMinutesCaseEntryId, x.OriginalFileName, x.ContentType, x.Content))
                .ToListAsync(ct);

        var attachmentsByEntryId = entryAttachments
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => (x.Item2, x.Item3, x.Item4)).ToList());

        var seq = await _pdfSequence.AllocateNextAsync(id, PdfDocumentType.Minutes, ct);

        var pdf = new SimplePdfWriter();
        pdf.Title($"Referat / Minutes — {meeting.MeetingDate} — {meeting.Year}/{meeting.YearSequenceNumber} — PDF v{seq}");
        if (!string.IsNullOrWhiteSpace(meeting.Location))
            pdf.Paragraph($"Sted: {meeting.Location}");

        pdf.Blank();

        pdf.Heading("Møtenotater");
        if (!string.IsNullOrWhiteSpace(minutes.AttendanceText)) pdf.Paragraph($"Oppmøte: {minutes.AttendanceText}");
        if (!string.IsNullOrWhiteSpace(minutes.AbsenceText)) pdf.Paragraph($"Forfall: {minutes.AbsenceText}");
        if (!string.IsNullOrWhiteSpace(minutes.ApprovalOfPreviousMinutesText)) pdf.Paragraph($"Godkjenning av forrige referat: {minutes.ApprovalOfPreviousMinutesText}");

        if (minutes.NextMeetingDate is not null)
        {
            pdf.Paragraph($"Neste møte: {minutes.NextMeetingDate}");
        }
        else
        {
            pdf.Paragraph("Neste møte: Dato ikke fastsatt");
        }

        pdf.Blank(12);
        pdf.Heading("Saker");

        var i = 1;
        foreach (var row in entries)
        {
            var assignee = userDisplay.TryGetValue(row.c.AssigneeUserId, out var d) ? d : row.c.AssigneeUserId;

            var outcomeDisplay = row.e.Outcome switch
            {
                MeetingCaseOutcome.Continue => "Fortsetter",
                MeetingCaseOutcome.Closed => "Avsluttet",
                MeetingCaseOutcome.Deferred => "Utsatt",
                MeetingCaseOutcome.Info => "Orientering",
                _ => row.e.Outcome.ToString()
            };

            pdf.Heading($"{i}. #{row.c.CaseNumber} — {row.c.Title} ({assignee})");
            pdf.Paragraph($"Utfallet: {outcomeDisplay}");

            if (!string.IsNullOrWhiteSpace(row.e.OfficialNotes))
                pdf.Paragraph($"Notater: {row.e.OfficialNotes}");

            if (!string.IsNullOrWhiteSpace(row.e.DecisionText))
                pdf.Paragraph($"Vedtak: {row.e.DecisionText}");

            if (!string.IsNullOrWhiteSpace(row.e.FollowUpText))
                pdf.Paragraph($"Oppfølging: {row.e.FollowUpText}");

            pdf.Blank(10);
            i++;
        }

        if (!string.IsNullOrWhiteSpace(minutes.EventueltText))
        {
            pdf.Blank(10);
            pdf.Heading("Eventuelt");
            pdf.Paragraph(minutes.EventueltText);
        }

        // Collect and embed all attachments
        var allAttachments = entryAttachments.Distinct().ToList();
        if (allAttachments.Count > 0)
        {
            pdf.Blank(12);
            pdf.Heading("Vedlegg");

            var attachmentNumber = 1;
            foreach (var att in allAttachments)
            {
                var (attachmentFileName, contentType, content) = (att.Item2, att.Item3, att.Item4);

                if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    pdf.AddPdfAttachment(content, attachmentFileName, attachmentNumber);
                }
                else if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    pdf.AddImageAttachment(content, attachmentFileName, attachmentNumber);
                }
                attachmentNumber++;
            }
        }

        var bytes = pdf.ToBytes();

        await _audit.LogAsync(
            AuditAction.GeneratePdf,
            entityType: nameof(Meeting),
            entityId: meeting.Id.ToString(),
            before: null,
            after: new { DocumentType = "Minutes", SequenceNumber = seq, meeting.Id, meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber },
            reason: "Generated minutes PDF",
            ct: ct);

        var fileName = $"minutes-{meeting.Year}-{meeting.YearSequenceNumber:00}-{meeting.MeetingDate}-v{seq}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private const long MaxSignedMinutesUploadBytes = 20 * 1024 * 1024; // 20 MB

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSignedMinutes(int meetingId, IFormFile file, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null) return NotFound();

        if (file is null || file.Length <= 0)
            return RedirectToAction(nameof(Minutes), new { id = meetingId });

        if (file.Length > MaxSignedMinutesUploadBytes)
            return BadRequest($"File too large. Max is {MaxSignedMinutesUploadBytes} bytes.");

        var contentType = file.ContentType ?? "application/octet-stream";
        if (!contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Signed minutes must be a PDF.");

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
            UploadedByUserId = _audit.GetActorUserId() ?? ""
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        var link = new MeetingMinutesAttachment
        {
            MeetingId = meetingId,
            AttachmentId = attachment.Id
        };

        _db.MeetingMinutesAttachments.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.UploadSignedMinutes,
            nameof(Meeting),
            meeting.Id.ToString(),
            before: null,
            after: new
            {
                MeetingId = meeting.Id,
                AttachmentId = attachment.Id,
                attachment.OriginalFileName,
                attachment.SizeBytes
            },
            reason: "Uploaded signed/scanned minutes",
            ct: ct);

        return RedirectToAction(nameof(Minutes), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAgendaAttachment(int meetingCaseId, IFormFile file, CancellationToken ct)
    {
        var mc = await _db.MeetingCases.FirstOrDefaultAsync(x => x.Id == meetingCaseId, ct);
        if (mc is null) return NotFound();

        if (file is null || file.Length <= 0)
            return RedirectToAction(nameof(Details), new { id = mc.MeetingId });

        if (file.Length > CasesController.MaxUploadBytes)
            return BadRequest($"File too large. Max is {CasesController.MaxUploadBytes} bytes.");

        var contentType = file.ContentType ?? "application/octet-stream";
        if (!CasesController.IsAllowedContentType(contentType))
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
            UploadedByUserId = _audit.GetActorUserId() ?? ""
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        var link = new MeetingCaseAttachment
        {
            MeetingCaseId = meetingCaseId,
            AttachmentId = attachment.Id
        };

        _db.MeetingCaseAttachments.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(MeetingCaseAttachment),
            link.Id.ToString(),
            before: null,
            after: new { link.Id, link.MeetingCaseId, link.AttachmentId },
            reason: "Uploaded agenda attachment",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = mc.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadMinutesEntryAttachment(int meetingMinutesCaseEntryId, IFormFile file, CancellationToken ct)
    {
        var entry = await _db.MeetingMinutesCaseEntries.FirstOrDefaultAsync(x => x.Id == meetingMinutesCaseEntryId, ct);
        if (entry is null) return NotFound();

        if (file is null || file.Length <= 0)
            return RedirectToAction(nameof(Minutes), new { id = entry.MeetingId });

        if (file.Length > CasesController.MaxUploadBytes)
            return BadRequest($"File too large. Max is {CasesController.MaxUploadBytes} bytes.");

        var contentType = file.ContentType ?? "application/octet-stream";
        if (!CasesController.IsAllowedContentType(contentType))
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
            UploadedByUserId = _audit.GetActorUserId() ?? ""
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        var link = new MeetingMinutesCaseEntryAttachment
        {
            MeetingMinutesCaseEntryId = meetingMinutesCaseEntryId,
            AttachmentId = attachment.Id
        };

        _db.MeetingMinutesCaseEntryAttachments.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(MeetingMinutesCaseEntryAttachment),
            link.Id.ToString(),
            before: null,
            after: new { link.Id, link.MeetingMinutesCaseEntryId, link.AttachmentId },
            reason: "Uploaded minutes entry attachment",
            ct: ct);

        return RedirectToAction(nameof(Minutes), new { id = entry.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMinutesEntryAttachment(int linkId, CancellationToken ct)
    {
        var link = await _db.MeetingMinutesCaseEntryAttachments
            .Include(x => x.MeetingMinutesCaseEntry)
            .FirstOrDefaultAsync(x => x.Id == linkId, ct);

        if (link is null) return NotFound();

        var before = new { link.Id, link.MeetingMinutesCaseEntryId, link.AttachmentId, link.IsDeleted, link.DeletedAt, link.DeletedByUserId };

        link.IsDeleted = true;
        link.DeletedAt = DateTimeOffset.UtcNow;
        link.DeletedByUserId = _audit.GetActorUserId();

        await _db.SaveChangesAsync(ct);

        var after = new { link.Id, link.MeetingMinutesCaseEntryId, link.AttachmentId, link.IsDeleted, link.DeletedAt, link.DeletedByUserId };

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(MeetingMinutesCaseEntryAttachment),
            link.Id.ToString(),
            before,
            after,
            reason: "Unlinked minutes entry attachment",
            ct: ct);

        return RedirectToAction(nameof(Minutes), new { id = link.MeetingMinutesCaseEntry.MeetingId });
    }
}
