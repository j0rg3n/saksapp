using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public record AgendaAttachmentRef(string FileName, string ContentType, byte[] Content);

public record AgendaCommentData(
    DateTime CreatedAt,
    string Text,
    IReadOnlyList<AgendaAttachmentRef> Attachments);

public record PreviousMinutesData(
    DateOnly MeetingDate,
    string? OfficialNotes,
    string? DecisionText,
    string? FollowUpText,
    IReadOnlyList<AgendaAttachmentRef> Attachments);

public record AgendaItemData(
    MeetingCase MeetingCase,
    BoardCase Case,
    string? AssigneeName,
    PreviousMinutesData? PreviousMinutes,
    IReadOnlyList<AgendaCommentData> CommentsBetweenMeetings);

public record AgendaPdfData(
    Meeting Meeting,
    int Sequence,
    IReadOnlyList<AgendaItemData> Items,
    IReadOnlyDictionary<string, int> AttachmentNumberByFileName,
    IReadOnlyList<AgendaAttachmentRef> AttachmentsInOrder);

public interface IAgendaPdfDataService
{
    Task<AgendaPdfData?> GetAgendaDataAsync(int meetingId, CancellationToken ct = default);
}

public sealed class AgendaPdfDataService : IAgendaPdfDataService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPdfSequenceService _pdfSequence;

    public AgendaPdfDataService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPdfSequenceService pdfSequence)
    {
        _db = db;
        _userManager = userManager;
        _pdfSequence = pdfSequence;
    }

    public async Task<AgendaPdfData?> GetAgendaDataAsync(int meetingId, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null) return null;

        var agenda = await _db.MeetingCases.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
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

        var seq = await _pdfSequence.AllocateNextAsync(meetingId, PdfDocumentType.Agenda, ct);

        var caseIds = agenda.Select(x => x.c.Id).Distinct().ToList();

        // Previous minutes entries (last before this meeting date)
        var previousMinutesCandidates = await _db.MeetingMinutesCaseEntries.AsNoTracking()
            .Where(x => caseIds.Contains(x.BoardCaseId))
            .Join(_db.Meetings.AsNoTracking(),
                e => e.MeetingId,
                m => m.Id,
                (e, m) => new { e.Id, e.BoardCaseId, m.MeetingDate, e.OfficialNotes, e.DecisionText, e.FollowUpText })
            .Where(x => x.MeetingDate < meeting.MeetingDate)
            .ToListAsync(ct);

        var previousByCaseId = previousMinutesCandidates
            .GroupBy(x => x.BoardCaseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.MeetingDate).ThenByDescending(x => x.Id).First());

        // Attachments for previous minutes entries
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
            .ToDictionary(g => g.Key, g => g.Select(x => new AgendaAttachmentRef(x.Item2, x.Item3, x.Item4)).ToList());

        // All comments for these cases
        var allComments = await _db.CaseComments.AsNoTracking()
            .Where(x => caseIds.Contains(x.BoardCaseId))
            .ToListAsync(ct);

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
            .ToDictionary(g => g.Key, g => g.Select(x => new AgendaAttachmentRef(x.Item2, x.Item3, x.Item4)).ToList());

        // Collect referenced attachments in order (de-duplicated by filename)
        var referencedAttachments = new Dictionary<string, AgendaAttachmentRef>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in agenda)
        {
            if (previousByCaseId.TryGetValue(row.c.Id, out var prev)
                && prevMinutesAttachmentsByEntryId.TryGetValue(prev.Id, out var prevAtts))
            {
                foreach (var att in prevAtts)
                    referencedAttachments.TryAdd(att.FileName, att);
            }

            var windowStart = previousByCaseId.TryGetValue(row.c.Id, out var p2)
                ? new DateTimeOffset(p2.MeetingDate.Year, p2.MeetingDate.Month, p2.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero)
                : DateTimeOffset.MinValue;
            var windowEnd = new DateTimeOffset(meeting.MeetingDate.Year, meeting.MeetingDate.Month, meeting.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero);

            var between = allComments
                .Where(c => c.BoardCaseId == row.c.Id && c.CreatedAt > windowStart && c.CreatedAt <= windowEnd)
                .ToList();

            foreach (var com in between)
            {
                if (commentAttachmentsByCommentId.TryGetValue(com.Id, out var atts))
                    foreach (var att in atts)
                        referencedAttachments.TryAdd(att.FileName, att);
            }
        }

        var attachmentNumberByFileName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var num = 1;
        foreach (var key in referencedAttachments.Keys)
            attachmentNumberByFileName[key] = num++;

        // Assemble per-item data
        var items = new List<AgendaItemData>();
        foreach (var row in agenda)
        {
            var assigneeName = userDisplay.TryGetValue(row.c.AssigneeUserId ?? "", out var d) ? d : row.c.AssigneeUserId;

            PreviousMinutesData? previousMinutes = null;
            if (previousByCaseId.TryGetValue(row.c.Id, out var prev))
            {
                var prevAtts = prevMinutesAttachmentsByEntryId.TryGetValue(prev.Id, out var list) ? list : new List<AgendaAttachmentRef>();
                previousMinutes = new PreviousMinutesData(prev.MeetingDate, prev.OfficialNotes, prev.DecisionText, prev.FollowUpText, prevAtts);
            }

            var windowStart = previousMinutes is not null
                ? new DateTimeOffset(previousMinutes.MeetingDate.Year, previousMinutes.MeetingDate.Month, previousMinutes.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero)
                : DateTimeOffset.MinValue;
            var windowEnd = new DateTimeOffset(meeting.MeetingDate.Year, meeting.MeetingDate.Month, meeting.MeetingDate.Day, 12, 0, 0, TimeSpan.Zero);

            var between = allComments
                .Where(c => c.BoardCaseId == row.c.Id && c.CreatedAt > windowStart && c.CreatedAt <= windowEnd)
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Select(c =>
                {
                    var atts = commentAttachmentsByCommentId.TryGetValue(c.Id, out var al) ? al : new List<AgendaAttachmentRef>();
                    return new AgendaCommentData(c.CreatedAt.DateTime, c.Text, atts);
                })
                .ToList();

            items.Add(new AgendaItemData(row.mc, row.c, assigneeName, previousMinutes, between));
        }

        return new AgendaPdfData(
            meeting,
            seq,
            items,
            attachmentNumberByFileName,
            referencedAttachments.Values.ToList());
    }
}
