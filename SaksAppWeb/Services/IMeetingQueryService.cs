using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;

namespace SaksAppWeb.Services;

public interface IMeetingQueryService
{
    Task<IReadOnlyList<Meeting>> GetAllMeetingsAsync(CancellationToken ct);
    Task<MeetingDetailsVm?> GetMeetingWithAgendaAsync(int id, CancellationToken ct);
    Task<MeetingMinutesVm?> GetMeetingWithMinutesAsync(int id, CancellationToken ct);
}

public class MeetingQueryService : IMeetingQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public MeetingQueryService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    public async Task<IReadOnlyList<Meeting>> GetAllMeetingsAsync(CancellationToken ct)
    {
        return await _db.Meetings.AsNoTracking()
            .OrderByDescending(x => x.MeetingDate)
            .ToListAsync(ct);
    }

    public async Task<MeetingDetailsVm?> GetMeetingWithAgendaAsync(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (meeting is null) return null;

        // Load MeetingEventLinks for this meeting, including CaseEvent -> Cases -> BoardCase
        var agendaEntities = await _db.MeetingEventLinks.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.Cases)
                    .ThenInclude(cec => cec.BoardCase)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var agendaRows = agendaEntities
            .Select(mel =>
            {
                var cec = mel.CaseEvent.Cases.FirstOrDefault();
                return cec is null ? null : new { mel, cec, boardCase = cec.BoardCase };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        var assigneeIds = agendaRows
            .Select(x => x.boardCase.AssigneeUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var agenda = agendaRows.Select(x => new MeetingAgendaRowVm
        {
            MeetingEventLinkId = x.mel.Id,
            AgendaOrder = x.mel.AgendaOrder,
            CaseId = x.boardCase.Id,
            CaseNumber = x.boardCase.CaseNumber,
            Title = x.boardCase.Title,
            AssigneeDisplay = userDisplay.TryGetValue(x.boardCase.AssigneeUserId ?? "", out var d) ? d : x.boardCase.AssigneeUserId,
            AgendaTextSnapshot = x.mel.AgendaTextSnapshot,
            TidsfristOverrideDate = x.mel.TidsfristOverrideDate,
            TidsfristOverrideText = x.mel.TidsfristOverrideText
        }).ToList();

        var alreadyScheduledCaseIds = agendaRows.Select(x => x.boardCase.Id).Distinct().ToList();

        var openCases = await _db.BoardCases.AsNoTracking()
            .Where(x => x.Status != CaseStatus.Closed)
            .Where(x => !alreadyScheduledCaseIds.Contains(x.Id))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CaseNumber)
            .Select(x => new { x.Id, x.CaseNumber, x.Title })
            .ToListAsync(ct);

        return new MeetingDetailsVm
        {
            Meeting = meeting,
            Agenda = agenda,
            OpenCasesToAdd = openCases
                .Select(c => new SelectListItem($"#{c.CaseNumber} — {c.Title}", c.Id.ToString()))
                .ToList()
        };
    }

    public async Task<MeetingMinutesVm?> GetMeetingWithMinutesAsync(int id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (meeting is null) return null;

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

        // Load MeetingEventLinks for this meeting, including CaseEvent -> Cases -> BoardCase
        var links = await _db.MeetingEventLinks.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.Cases)
                    .ThenInclude(cec => cec.BoardCase)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var assigneeIds = links
            .SelectMany(mel => mel.CaseEvent.Cases.Select(cec => cec.BoardCase.AssigneeUserId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var caseEventIds = links.Select(x => x.CaseEventId).Distinct().ToList();

        // Load attachments for all case events in one query
        var attachmentRows = caseEventIds.Count == 0
            ? new List<(int CaseEventId, int LinkId, int AttachmentId, string FileName, string ContentType, long SizeBytes)>()
            : await _db.CaseEventAttachments.AsNoTracking()
                .Where(x => caseEventIds.Contains(x.CaseEventId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new
                    {
                        link.CaseEventId,
                        LinkId = link.Id,
                        AttachmentId = att.Id,
                        att.OriginalFileName,
                        att.ContentType,
                        att.SizeBytes
                    })
                .OrderByDescending(x => x.AttachmentId)
                .Select(x => new ValueTuple<int, int, int, string, string, long>(
                    x.CaseEventId, x.LinkId, x.AttachmentId, x.OriginalFileName, x.ContentType, x.SizeBytes))
                .ToListAsync(ct);

        var attachmentsByCaseEventId = attachmentRows
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.ToList());

        var caseEntries = new List<MeetingMinutesCaseEntryVm>();
        foreach (var mel in links)
        {
            var cec = mel.CaseEvent.Cases.FirstOrDefault();
            if (cec is null) continue;

            var boardCase = cec.BoardCase;
            var atts = attachmentsByCaseEventId.TryGetValue(mel.CaseEventId, out var rawAtts) ? rawAtts : new();

            var attachmentVms = atts.Select(a => new MinutesEntryAttachmentVm
            {
                LinkId = a.Item2,
                AttachmentId = a.Item3,
                OriginalFileName = a.Item4,
                ContentType = a.Item5,
                SizeBytes = a.Item6
            }).ToList();

            caseEntries.Add(new MeetingMinutesCaseEntryVm
            {
                MeetingEventLinkId = mel.Id,
                CaseEventId = mel.CaseEventId,
                BoardCaseId = boardCase.Id,
                CaseNumber = boardCase.CaseNumber,
                Title = boardCase.Title,
                AssigneeDisplay = userDisplay.TryGetValue(boardCase.AssigneeUserId ?? "", out var d) ? d : boardCase.AssigneeUserId,
                OfficialNotes = mel.OfficialNotes,
                DecisionText = mel.DecisionText,
                FollowUpText = mel.FollowUpText,
                Outcome = mel.Outcome ?? MeetingCaseOutcome.Continue,
                Attachments = attachmentVms
            });
        }

        var vm = new MeetingMinutesVm
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

            CaseEntries = caseEntries
        };

        var signed = await _db.MeetingMinutesAttachments.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .Join(_db.Attachments.AsNoTracking(),
                link => link.AttachmentId,
                att => att.Id,
                (link, att) => new SignedMinutesVm
                {
                    AttachmentId = att.Id,
                    OriginalFileName = att.OriginalFileName,
                    SizeBytes = att.SizeBytes
                })
            .OrderByDescending(x => x.AttachmentId)
            .ToListAsync(ct);

        vm.SignedMinutes = signed;

        return vm;
    }
}