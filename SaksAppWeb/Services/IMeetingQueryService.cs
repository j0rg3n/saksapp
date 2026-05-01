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
        }).ToList();

        var alreadyScheduledCaseIds = agendaEntities.Select(x => x.c.Id).Distinct().ToList();

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

        var agenda = await _db.MeetingCases.AsNoTracking()
            .Where(x => x.MeetingId == id)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var agendaCaseIds = agenda.Select(x => x.Id).ToList();

        var existingEntries = await _db.MeetingMinutesCaseEntries
            .Where(x => x.MeetingId == id)
            .ToListAsync(ct);

        var existingByMeetingCaseId = existingEntries.ToDictionary(x => x.MeetingCaseId, x => x);

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

            CaseEntries = entries.Select(x => new MeetingMinutesCaseEntryVm
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
                (link, att) => new SignedMinutesVm
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
            ? new List<(int EntryId, MinutesEntryAttachmentVm Att)>()
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
                .Select(x => new ValueTuple<int, MinutesEntryAttachmentVm>(
                    x.MeetingMinutesCaseEntryId,
                    new MinutesEntryAttachmentVm
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

        return vm;
    }
}