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
    private readonly IMeetingQueryService _meetingQuery;
    private readonly ISimplePdfWriterFactory _pdfFactory;
    private readonly IAgendaPdfDataService _agendaPdfData;
    private readonly IMinutesPdfDataService _minutesPdfData;
    private readonly IMinutesSaveService _minutesSave;

    public MeetingsController(
        ApplicationDbContext db,
        IAuditService audit,
        UserManager<ApplicationUser> userManager,
        IPdfSequenceService pdfSequence,
        IMeetingQueryService meetingQuery,
        ISimplePdfWriterFactory pdfFactory,
        IAgendaPdfDataService agendaPdfData,
        IMinutesPdfDataService minutesPdfData,
        IMinutesSaveService minutesSave)
    {
        _db = db;
        _audit = audit;
        _userManager = userManager;
        _pdfSequence = pdfSequence;
        _meetingQuery = meetingQuery;
        _pdfFactory = pdfFactory;
        _agendaPdfData = agendaPdfData;
        _minutesPdfData = minutesPdfData;
        _minutesSave = minutesSave;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var meetings = await _meetingQuery.GetAllMeetingsAsync(ct);

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

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FindAsync(new object[] { id }, ct);
        if (meeting is null) return NotFound();

        var vm = new MeetingEditVm
        {
            Id = meeting.Id,
            MeetingDate = meeting.MeetingDate,
            YearSequenceNumber = meeting.YearSequenceNumber,
            Location = meeting.Location
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MeetingEditVm vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var meeting = await _db.Meetings.FindAsync(new object[] { vm.Id }, ct);
        if (meeting is null) return NotFound();

        var before = new { meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber, meeting.Location };

        meeting.MeetingDate = vm.MeetingDate;
        meeting.Year = vm.MeetingDate.Year;
        meeting.YearSequenceNumber = vm.YearSequenceNumber;
        meeting.Location = vm.Location;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(Meeting),
            meeting.Id.ToString(),
            before: before,
            after: new { meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber, meeting.Location },
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = meeting.Id });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var vm = await _meetingQuery.GetMeetingWithAgendaAsync(id, ct);
        if (vm is null) return NotFound();

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
        var data = await _agendaPdfData.GetAgendaDataAsync(id, ct);
        if (data is null) return NotFound();

        var meeting = data.Meeting;
        var seq = data.Sequence;

        // --- Build PDF ---
        var pdf = _pdfFactory.Create();
        pdf.Title($"Innkalling styremøte #{meeting.YearSequenceNumber} {meeting.Year} — {meeting.MeetingDate:dd.MM.yyyy}");
        pdf.Paragraph($"(v{seq})");
        if (!string.IsNullOrWhiteSpace(meeting.Location))
            pdf.Paragraph($"Sted: {meeting.Location}");

        pdf.Blank();
        pdf.Heading("Agenda");
        pdf.Heading2("1. Godkjenne forrige referat.");

        var agendaNumber = 2;
        foreach (var item in data.Items)
        {
            var assignee = item.AssigneeName ?? item.Case.AssigneeUserId ?? "";
            pdf.Heading2($"{agendaNumber}. {item.Case.Title} ({assignee}; #{item.Case.CaseNumber})");

            if (!string.IsNullOrWhiteSpace(item.Case.Description))
                pdf.ParagraphItalic(item.Case.Description);

            if (!string.IsNullOrWhiteSpace(item.MeetingCase.AgendaTextSnapshot) &&
                !item.MeetingCase.AgendaTextSnapshot.Equals(item.Case.Description, StringComparison.OrdinalIgnoreCase))
                pdf.Paragraph(item.MeetingCase.AgendaTextSnapshot);

            var tidsfrist = (item.MeetingCase.TidsfristOverrideDate is not null || !string.IsNullOrWhiteSpace(item.MeetingCase.TidsfristOverrideText))
                ? $"{item.MeetingCase.TidsfristOverrideDate?.ToString() ?? ""} {item.MeetingCase.TidsfristOverrideText ?? ""}".Trim()
                : (item.Case.CustomTidsfristDate is not null || !string.IsNullOrWhiteSpace(item.Case.CustomTidsfristText))
                    ? $"{item.Case.CustomTidsfristDate?.ToString() ?? ""} {item.Case.CustomTidsfristText ?? ""}".Trim()
                    : "Innen neste møte";

            if (item.PreviousMinutes is { } prev)
            {
                pdf.Heading3($"Forrige møte ({prev.MeetingDate:dd.MM.yyyy}):");
                if (!string.IsNullOrWhiteSpace(prev.OfficialNotes)) pdf.HeadingInline("Status: ", prev.OfficialNotes);
                if (!string.IsNullOrWhiteSpace(prev.DecisionText)) pdf.HeadingInline("Vedtak: ", prev.DecisionText);
                if (!string.IsNullOrWhiteSpace(prev.FollowUpText)) pdf.HeadingInline("Oppfølging: ", prev.FollowUpText);
                if (!string.IsNullOrWhiteSpace(tidsfrist)) pdf.HeadingInline("Tidsfrist: ", tidsfrist);
                if (prev.Attachments.Count > 0)
                {
                    var attRefs = string.Join(", ", prev.Attachments.Select(a => $"[Vedlegg {data.AttachmentNumberByFileName[a.FileName]}]"));
                    pdf.WriteTextWithAttachmentLinks($"Vedlegg: {attRefs}");
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(tidsfrist)) pdf.HeadingInline("Tidsfrist: ", tidsfrist);
            }

            var tilMotet = !string.IsNullOrWhiteSpace(item.MeetingCase.FollowUpTextDraft)
                ? item.MeetingCase.FollowUpTextDraft
                : item.MeetingCase.AgendaTextSnapshot;
            if (!string.IsNullOrWhiteSpace(tilMotet) && !tilMotet.Equals(item.Case.Description, StringComparison.OrdinalIgnoreCase))
            {
                pdf.Heading3("Til møtet:");
                pdf.ParagraphIndented(tilMotet);
            }

            if (item.CommentsBetweenMeetings.Count > 0)
            {
                pdf.Heading3("Historikk siden forrige møte:");
                foreach (var com in item.CommentsBetweenMeetings)
                {
                    var fullText = $"{com.CreatedAt:yyyy-MM-dd}: {com.Text}";
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
                        if (splitIdx > 10)
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
                    if (com.Attachments.Count > 0)
                    {
                        var attRefs = string.Join(", ", com.Attachments.Select(a => $"[Vedlegg {data.AttachmentNumberByFileName[a.FileName]}]"));
                        pdf.WriteTextWithAttachmentLinks(attRefs);
                    }
                }
            }

            pdf.Blank(10);
            agendaNumber++;
        }

        if (data.AttachmentsInOrder.Count > 0)
        {
            pdf.Blank(6);
            pdf.Heading2("Vedlegg");
            var attachmentNumber = 1;
            foreach (var att in data.AttachmentsInOrder)
            {
                pdf.Paragraph($"Vedlegg {attachmentNumber}: {att.FileName}");
                attachmentNumber++;
            }
            attachmentNumber = 1;
            foreach (var att in data.AttachmentsInOrder)
            {
                if (att.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    pdf.AddPdfAttachment(att.Content, att.FileName, attachmentNumber);
                else if (att.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    pdf.AddImageAttachment(att.Content, att.FileName, attachmentNumber);
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

        var pdfFileName = $"{meeting.Year}-#{meeting.YearSequenceNumber}-innkalling-{meeting.MeetingDate:dd.MM.yyyy}-v{seq}.pdf";
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

        var pdf = _pdfFactory.Create();
        pdf.Title($"Påminnelse per ansvarlig — {meeting.MeetingDate:dd.MM.yyyy} — {meeting.Year}/{meeting.YearSequenceNumber} — v{seq}");
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

        var fileName = $"reminder-{meeting.Year}-{meeting.YearSequenceNumber:00}-{meeting.MeetingDate:dd.MM.yyyy}-v{seq}.pdf";
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
        var saved = await _minutesSave.SaveMinutesAsync(vm, ct);
        if (!saved) return NotFound();
        return RedirectToAction(nameof(Minutes), new { id = vm.MeetingId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadMinutesPdf(int id, CancellationToken ct)
    {
        var data = await _minutesPdfData.GetMinutesDataAsync(id, ct);
        if (data is null) return NotFound();

        var meeting = data.Meeting;
        var minutes = data.Minutes;
        var seq = data.Sequence;

        var pdf = _pdfFactory.Create();
        pdf.Title($"Referat — {meeting.MeetingDate:dd.MM.yyyy} — {meeting.Year}/{meeting.YearSequenceNumber} — v{seq}");
        if (!string.IsNullOrWhiteSpace(meeting.Location))
            pdf.Paragraph($"Sted: {meeting.Location}");

        pdf.Blank();

        if (!string.IsNullOrWhiteSpace(minutes.AttendanceText)) pdf.Paragraph($"Oppmøte: {minutes.AttendanceText}");
        if (!string.IsNullOrWhiteSpace(minutes.AbsenceText)) pdf.Paragraph($"Forfall: {minutes.AbsenceText}");

        pdf.Paragraph(minutes.NextMeetingDate is not null
            ? $"Neste møte: {minutes.NextMeetingDate:dd.MM.yyyy}"
            : "Neste møte: Dato ikke fastsatt");

        pdf.Blank(12);
        pdf.Heading("Saker");

        if (!string.IsNullOrWhiteSpace(minutes.ApprovalOfPreviousMinutesText))
        {
            pdf.Heading("1. Godkjenning av forrige referat");
            pdf.Paragraph(minutes.ApprovalOfPreviousMinutesText);
            pdf.Blank(12);
        }

        var i = 2;
        foreach (var entry in data.Entries)
        {
            var assignee = entry.AssigneeName ?? entry.Case.AssigneeUserId ?? "";

            var outcomeDisplay = entry.Entry.Outcome switch
            {
                MeetingCaseOutcome.Continue => "Fortsetter",
                MeetingCaseOutcome.Closed => "Avsluttet",
                MeetingCaseOutcome.Deferred => "Utsatt",
                MeetingCaseOutcome.Info => "Orientering",
                _ => entry.Entry.Outcome.ToString()
            };

            pdf.Heading($"{i}. {entry.Case.Title} ({assignee}; #{entry.Case.CaseNumber})");
            pdf.Paragraph($"Status: {outcomeDisplay}");

            if (!string.IsNullOrWhiteSpace(entry.Entry.OfficialNotes)) pdf.Paragraph(entry.Entry.OfficialNotes);
            if (!string.IsNullOrWhiteSpace(entry.Entry.DecisionText)) pdf.Paragraph($"Vedtak: {entry.Entry.DecisionText}");
            if (!string.IsNullOrWhiteSpace(entry.Entry.FollowUpText)) pdf.Paragraph($"Oppfølging: {entry.Entry.FollowUpText}");

            if (entry.AttachmentNumbers.Count > 0)
                pdf.Paragraph(string.Join(", ", entry.AttachmentNumbers.Select(n => $"Vedlegg {n}")));

            pdf.Blank(10);
            i++;
        }

        if (!string.IsNullOrWhiteSpace(minutes.EventueltText))
        {
            pdf.Blank(10);
            pdf.Heading("Eventuelt");
            pdf.Paragraph(minutes.EventueltText);
        }

        if (data.AllAttachments.Count > 0)
        {
            pdf.Blank(12);
            pdf.Heading("Vedlegg");

            var pageNumbers = new List<int>();
            foreach (var _ in data.AllAttachments)
                pageNumbers.Add(pdf.GetCurrentPageNumber() + 1);

            var attachmentNumber = 1;
            foreach (var att in data.AllAttachments)
            {
                pdf.WriteAttachmentTocEntry(pageNumbers[attachmentNumber - 1], attachmentNumber, att.FileName);
                attachmentNumber++;
            }

            attachmentNumber = 1;
            foreach (var att in data.AllAttachments)
            {
                if (att.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    pdf.AddPdfAttachment(att.Content, att.FileName, attachmentNumber);
                else if (att.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    pdf.AddImageAttachment(att.Content, att.FileName, attachmentNumber);
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

        var fileName = $"minutes-{meeting.Year}-{meeting.YearSequenceNumber:00}-{meeting.MeetingDate:dd.MM.yyyy}-v{seq}.pdf";
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
