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

        // Check existence via MeetingEventLinks joined to CaseEventCases
        var exists = await _db.MeetingEventLinks
            .AnyAsync(x => x.MeetingId == meetingId
                && x.CaseEvent.Cases.Any(cec => cec.BoardCaseId == caseId), ct);
        if (exists) return RedirectToAction(nameof(Details), new { id = meetingId });

        var maxOrder = await _db.MeetingEventLinks
            .Where(x => x.MeetingId == meetingId)
            .MaxAsync(x => (int?)x.AgendaOrder, ct);

        var agendaText = !string.IsNullOrWhiteSpace(boardCase.Description)
            ? boardCase.Description!
            : boardCase.Title;

        // Create CaseEvent (Category="meeting")
        var caseEvent = new CaseEvent
        {
            Category = "meeting",
            Content = "",
            CreatedAt = new DateTimeOffset(meeting.MeetingDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            CreatedByUserId = _audit.GetActorUserId()
        };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync(ct);

        // Create MeetingEventLink
        var mel = new MeetingEventLink
        {
            MeetingId = meetingId,
            CaseEventId = caseEvent.Id,
            AgendaOrder = (maxOrder ?? 0) + 1,
            AgendaTextSnapshot = agendaText,
            IsEventuelt = false
        };
        _db.MeetingEventLinks.Add(mel);

        // Create CaseEventCase
        var caseEventCase = new CaseEventCase
        {
            CaseEventId = caseEvent.Id,
            BoardCaseId = caseId
        };
        _db.CaseEventCases.Add(caseEventCase);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(MeetingEventLink),
            mel.Id.ToString(),
            before: null,
            after: new
            {
                mel.Id,
                mel.MeetingId,
                BoardCaseId = caseId,
                mel.AgendaOrder,
                mel.AgendaTextSnapshot,
                mel.TidsfristOverrideDate,
                mel.TidsfristOverrideText
            },
            reason: "Added case to meeting agenda",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEventueltItem(int meetingId, string? content, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null) return NotFound();

        if (string.IsNullOrWhiteSpace(content))
            return RedirectToAction(nameof(Minutes), new { id = meetingId });

        var maxOrder = await _db.MeetingEventLinks
            .Where(x => x.MeetingId == meetingId)
            .MaxAsync(x => (int?)x.AgendaOrder, ct);

        var caseEvent = new CaseEvent
        {
            Category = "meeting",
            Content = content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _audit.GetActorUserId()
        };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync(ct);

        var mel = new MeetingEventLink
        {
            MeetingId = meetingId,
            CaseEventId = caseEvent.Id,
            AgendaOrder = (maxOrder ?? 0) + 1,
            AgendaTextSnapshot = content.Trim(),
            IsEventuelt = true
        };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(MeetingEventLink),
            mel.Id.ToString(),
            before: null,
            after: new { mel.Id, meetingId, IsEventuelt = true, content = content.Trim() },
            reason: "Added eventuelt item",
            ct: ct);

        var totalCount = await _db.MeetingEventLinks
            .Where(x => x.MeetingId == meetingId)
            .CountAsync(ct);

        return RedirectToAction(nameof(Minutes), new { id = meetingId, index = totalCount - 1 });
    }

    public async Task<IActionResult> EditAgendaItem(int id, CancellationToken ct)
    {
        // id is MeetingEventLinkId
        var mel = await _db.MeetingEventLinks.AsNoTracking()
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.Cases)
                    .ThenInclude(cec => cec.BoardCase)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (mel is null) return NotFound();

        var boardCase = mel.CaseEvent.Cases.FirstOrDefault()?.BoardCase;
        if (boardCase is null) return NotFound();

        var vm = new MeetingCaseEditVm
        {
            Id = mel.Id,
            MeetingId = mel.MeetingId,
            BoardCaseId = boardCase.Id,
            CaseNumber = boardCase.CaseNumber,
            CaseTitle = boardCase.Title,
            AgendaTextSnapshot = mel.AgendaTextSnapshot,
            TidsfristOverrideDate = mel.TidsfristOverrideDate,
            TidsfristOverrideText = mel.TidsfristOverrideText
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAgendaItem(MeetingCaseEditVm vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        // vm.Id is MeetingEventLinkId
        var mel = await _db.MeetingEventLinks.FirstOrDefaultAsync(x => x.Id == vm.Id, ct);
        if (mel is null) return NotFound();

        var before = new
        {
            mel.AgendaTextSnapshot,
            mel.TidsfristOverrideDate,
            mel.TidsfristOverrideText
        };

        mel.AgendaTextSnapshot = vm.AgendaTextSnapshot;
        mel.TidsfristOverrideDate = vm.TidsfristOverrideDate;
        mel.TidsfristOverrideText = vm.TidsfristOverrideText;

        await _db.SaveChangesAsync(ct);

        var after = new
        {
            mel.AgendaTextSnapshot,
            mel.TidsfristOverrideDate,
            mel.TidsfristOverrideText
        };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(MeetingEventLink),
            mel.Id.ToString(),
            before,
            after,
            reason: "Edited agenda item",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = mel.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAgendaItem(int id, CancellationToken ct)
    {
        // id is MeetingEventLinkId
        var mel = await _db.MeetingEventLinks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (mel is null) return NotFound();

        var before = new { mel.Id, mel.MeetingId, mel.CaseEventId, mel.IsDeleted, mel.DeletedAt, mel.DeletedByUserId };

        mel.IsDeleted = true;
        mel.DeletedAt = DateTimeOffset.UtcNow;
        mel.DeletedByUserId = _audit.GetActorUserId();

        await _db.SaveChangesAsync(ct);

        var after = new { mel.Id, mel.MeetingId, mel.CaseEventId, mel.IsDeleted, mel.DeletedAt, mel.DeletedByUserId };

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(MeetingEventLink),
            mel.Id.ToString(),
            before,
            after,
            reason: "Removed agenda item (soft delete)",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = mel.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveAgendaItem(int id, bool up, CancellationToken ct)
    {
        // id is MeetingEventLinkId
        var mel = await _db.MeetingEventLinks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (mel is null) return NotFound();

        var meetingId = mel.MeetingId;

        var neighbor = await _db.MeetingEventLinks
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
            nameof(MeetingEventLink),
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

            var tilMotet = item.MeetingCase.AgendaTextSnapshot;
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

        var agendaLinks = await _db.MeetingEventLinks.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.Cases)
                    .ThenInclude(cec => cec.BoardCase)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var agendaRows = agendaLinks
            .Select(mel =>
            {
                var cec = mel.CaseEvent.Cases.FirstOrDefault();
                return cec is null ? null : new { mel, boardCase = cec.BoardCase };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        var assigneeIds = agendaRows.Select(x => x.boardCase.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var seq = await _pdfSequence.AllocateNextAsync(id, PdfDocumentType.AssigneeReminder, ct);

        var pdf = _pdfFactory.Create();
        pdf.Title($"Påminnelse per ansvarlig — {meeting.MeetingDate:dd.MM.yyyy} — {meeting.Year}/{meeting.YearSequenceNumber} — v{seq}");
        pdf.Paragraph("Kort liste per ansvarlig over saker til møtet.");

        pdf.Blank();

        var grouped = agendaRows
            .GroupBy(x => x.boardCase.AssigneeUserId)
            .OrderBy(g => userDisplay.TryGetValue(g.Key ?? "", out var d) ? d : g.Key);

        foreach (var g in grouped)
        {
            var assignee = userDisplay.TryGetValue(g.Key ?? "", out var d) ? d : g.Key;
            pdf.Heading(assignee);

            foreach (var row in g.OrderBy(x => x.mel.AgendaOrder))
            {
                var tidsfrist = (row.mel.TidsfristOverrideDate is not null || !string.IsNullOrWhiteSpace(row.mel.TidsfristOverrideText))
                    ? $"{row.mel.TidsfristOverrideDate?.ToString() ?? ""} {row.mel.TidsfristOverrideText ?? ""}".Trim()
                    : (row.boardCase.CustomTidsfristDate is not null || !string.IsNullOrWhiteSpace(row.boardCase.CustomTidsfristText))
                        ? $"{row.boardCase.CustomTidsfristDate?.ToString() ?? ""} {row.boardCase.CustomTidsfristText ?? ""}".Trim()
                        : "Innen neste møte";

                pdf.Paragraph($"- #{row.boardCase.CaseNumber} {row.boardCase.Title} — Tidsfrist: {tidsfrist}");
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

    public async Task<IActionResult> Minutes(int id, int index = 0, CancellationToken ct = default)
    {
        var vm = await _meetingQuery.GetMeetingWithMinutesAsync(id, ct);
        if (vm is null) return NotFound();

        vm.TotalCount = vm.CaseEntries.Count;
        vm.CurrentIndex = Math.Clamp(index, 0, Math.Max(0, vm.CaseEntries.Count - 1));

        if (vm.CaseEntries.Count > 0)
            vm.CaseEntries = [vm.CaseEntries[vm.CurrentIndex]];

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Minutes(SaksAppWeb.Models.ViewModels.MeetingMinutesVm vm, string? action, CancellationToken ct)
    {
        var saved = await _minutesSave.SaveMinutesAsync(vm, ct);
        if (!saved) return NotFound();

        if (action == "next")
        {
            var nextIndex = vm.CurrentIndex + 1;
            if (nextIndex < vm.TotalCount)
                return RedirectToAction(nameof(Minutes), new { id = vm.MeetingId, index = nextIndex });
            return RedirectToAction(nameof(Details), new { id = vm.MeetingId });
        }

        return RedirectToAction(nameof(Minutes), new { id = vm.MeetingId, index = vm.CurrentIndex });
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
                MeetingCaseOutcome.Orientering => "Orientering",
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
        // meetingCaseId is now meetingEventLinkId
        var mel = await _db.MeetingEventLinks.FirstOrDefaultAsync(x => x.Id == meetingCaseId, ct);
        if (mel is null) return NotFound();

        if (file is null || file.Length <= 0)
            return RedirectToAction(nameof(Details), new { id = mel.MeetingId });

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

        var link = new CaseEventAttachment
        {
            CaseEventId = mel.CaseEventId,
            AttachmentId = attachment.Id
        };

        _db.CaseEventAttachments.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(CaseEventAttachment),
            link.Id.ToString(),
            before: null,
            after: new { link.Id, MeetingEventLinkId = mel.Id, link.CaseEventId, link.AttachmentId },
            reason: "Uploaded agenda attachment",
            ct: ct);

        return RedirectToAction(nameof(Details), new { id = mel.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadMinutesEntryAttachment(int meetingMinutesCaseEntryId, IFormFile file, CancellationToken ct)
    {
        // meetingMinutesCaseEntryId is now a CaseEventId
        var caseEvent = await _db.CaseEvents
            .Include(x => x.MeetingLink)
            .FirstOrDefaultAsync(x => x.Id == meetingMinutesCaseEntryId, ct);
        if (caseEvent is null) return NotFound();

        var meetingId = caseEvent.MeetingLink?.MeetingId;

        if (file is null || file.Length <= 0)
            return RedirectToAction(nameof(Minutes), new { id = meetingId });

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

        var link = new CaseEventAttachment
        {
            CaseEventId = meetingMinutesCaseEntryId,
            AttachmentId = attachment.Id
        };

        _db.CaseEventAttachments.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(CaseEventAttachment),
            link.Id.ToString(),
            before: null,
            after: new { link.Id, link.CaseEventId, link.AttachmentId },
            reason: "Uploaded minutes entry attachment",
            ct: ct);

        return RedirectToAction(nameof(Minutes), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMinutesEntryAttachment(int linkId, CancellationToken ct)
    {
        var link = await _db.CaseEventAttachments
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.MeetingLink)
            .FirstOrDefaultAsync(x => x.Id == linkId, ct);

        if (link is null) return NotFound();

        var before = new { link.Id, link.CaseEventId, link.AttachmentId, link.IsDeleted, link.DeletedAt, link.DeletedByUserId };

        link.IsDeleted = true;
        link.DeletedAt = DateTimeOffset.UtcNow;
        link.DeletedByUserId = _audit.GetActorUserId();

        await _db.SaveChangesAsync(ct);

        var after = new { link.Id, link.CaseEventId, link.AttachmentId, link.IsDeleted, link.DeletedAt, link.DeletedByUserId };

        await _audit.LogAsync(
            AuditAction.SoftDelete,
            nameof(CaseEventAttachment),
            link.Id.ToString(),
            before,
            after,
            reason: "Unlinked minutes entry attachment",
            ct: ct);

        var meetingId = link.CaseEvent.MeetingLink?.MeetingId;
        return RedirectToAction(nameof(Minutes), new { id = meetingId });
    }
}
