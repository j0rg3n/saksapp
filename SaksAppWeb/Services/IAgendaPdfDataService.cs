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
    TimeOnly? MeetingTime,
    string? OfficialNotes,
    string? DecisionText,
    string? FollowUpText,
    IReadOnlyList<AgendaAttachmentRef> Attachments);

public record AgendaItemData(
    MeetingEventLink MeetingCase,
    BoardCase Case,
    string? AssigneeName,
    PreviousMinutesData? PreviousMinutes,
    IReadOnlyList<AgendaCommentData> CommentsBetweenMeetings);

public record AgendaPdfData(
    Meeting Meeting,
    int Sequence,
    IReadOnlyList<AgendaItemData> Items,
    IReadOnlyDictionary<string, string> AttachmentLabelByFileName,
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

    /// <summary>
    /// The instant a meeting starts, used to bound "Historikk siden forrige møte". When no time is
    /// recorded we take the end of the day rather than an arbitrary hour, so a missing time can
    /// never silently drop an event registered later that day.
    /// </summary>
    internal static DateTimeOffset MeetingBoundary(DateOnly date, TimeOnly? time) =>
        new(date.ToDateTime(time ?? new TimeOnly(23, 59, 59)), TimeSpan.Zero);

    public async Task<AgendaPdfData?> GetAgendaDataAsync(int meetingId, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null) return null;

        // Load MeetingEventLinks for this meeting, including CaseEvent -> Cases -> BoardCase
        var agendaLinks = await _db.MeetingEventLinks.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
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

        var seq = await _pdfSequence.AllocateNextAsync(meetingId, PdfDocumentType.Agenda, ct);

        var caseIds = agendaRows.Select(x => x.boardCase.Id).Distinct().ToList();

        // Previous minutes entries — find most recent MeetingEventLink for each case before this meeting
        var previousMinutesCandidates = caseIds.Count == 0
            ? new List<(int CaseEventId, int BoardCaseId, DateOnly MeetingDate, TimeOnly? MeetingTime, string? OfficialNotes, string? DecisionText, string? FollowUpText)>()
            : await _db.MeetingEventLinks.AsNoTracking()
                .Include(x => x.Meeting)
                .Include(x => x.CaseEvent)
                    .ThenInclude(ce => ce.Cases)
                .Where(x => x.Meeting.MeetingDate < meeting.MeetingDate)
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .SelectMany(mel => mel.CaseEvent.Cases
                        .Where(cec => caseIds.Contains(cec.BoardCaseId))
                        .Select(cec => (
                            CaseEventId: mel.CaseEventId,
                            BoardCaseId: cec.BoardCaseId,
                            MeetingDate: mel.Meeting.MeetingDate,
                            MeetingTime: mel.Meeting.MeetingTime,
                            OfficialNotes: mel.OfficialNotes,
                            DecisionText: mel.DecisionText,
                            FollowUpText: mel.FollowUpText)))
                    .ToList(), TaskScheduler.Default);

        var previousByCaseId = previousMinutesCandidates
            .GroupBy(x => x.BoardCaseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.MeetingDate).ThenByDescending(x => x.CaseEventId).First());

        // Attachments for previous minutes entries (via CaseEventAttachments)
        var prevCaseEventIds = previousByCaseId.Values.Select(x => x.CaseEventId).Distinct().ToList();
        var prevMinutesAttachmentsFull = prevCaseEventIds.Count == 0
            ? new List<(int CaseEventId, string FileName, string ContentType, byte[] Content)>()
            : await _db.CaseEventAttachments.AsNoTracking()
                .Where(x => prevCaseEventIds.Contains(x.CaseEventId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new { link.CaseEventId, att.OriginalFileName, att.ContentType, att.Content })
                .Select(x => new ValueTuple<int, string, string, byte[]>(x.CaseEventId, x.OriginalFileName, x.ContentType, x.Content))
                .ToListAsync(ct);

        var prevMinutesAttachmentsByCaseEventId = prevMinutesAttachmentsFull
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => new AgendaAttachmentRef(x.Item2, x.Item3, x.Item4)).ToList());

        // All case-level CaseEvents for these cases. Must stay in step with the case timeline
        // in ICaseQueryService — filtering narrower here is what hid every post-Phase-4.5 event.
        var allCaseEvents = await _db.CaseEventCases.AsNoTracking()
            .Where(x => caseIds.Contains(x.BoardCaseId))
            .Include(x => x.CaseEvent)
            .Where(x => CaseEventCategories.CaseLevel.Contains(x.CaseEvent.Category))
            .ToListAsync(ct);

        var eventIds = allCaseEvents.Select(x => x.CaseEventId).Distinct().ToList();
        var eventAttachmentsFull = eventIds.Count == 0
            ? new List<(int CaseEventId, string FileName, string ContentType, byte[] Content)>()
            : await _db.CaseEventAttachments.AsNoTracking()
                .Where(x => eventIds.Contains(x.CaseEventId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new { link.CaseEventId, att.OriginalFileName, att.ContentType, att.Content })
                .Select(x => new ValueTuple<int, string, string, byte[]>(x.CaseEventId, x.OriginalFileName, x.ContentType, x.Content))
                .ToListAsync(ct);

        var eventAttachmentsByCaseEventId = eventAttachmentsFull
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => new AgendaAttachmentRef(x.Item2, x.Item3, x.Item4)).ToList());

        // Collect referenced attachments in order (de-duplicated by filename) with per-item labels
        var referencedAttachments = new Dictionary<string, AgendaAttachmentRef>(StringComparer.OrdinalIgnoreCase);
        var attachmentLabelByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in agendaRows)
        {
            var agendaOrder = row.mel.AgendaOrder;
            var attachmentIndex = 1;

            if (previousByCaseId.TryGetValue(row.boardCase.Id, out var prev)
                && prevMinutesAttachmentsByCaseEventId.TryGetValue(prev.CaseEventId, out var prevAtts))
            {
                foreach (var att in prevAtts)
                {
                    referencedAttachments.TryAdd(att.FileName, att);
                    if (!attachmentLabelByFileName.ContainsKey(att.FileName))
                        attachmentLabelByFileName[att.FileName] = $"{agendaOrder}.{attachmentIndex}";
                    attachmentIndex++;
                }
            }

            var windowStart = previousByCaseId.TryGetValue(row.boardCase.Id, out var p2)
                ? MeetingBoundary(p2.MeetingDate, p2.MeetingTime)
                : DateTimeOffset.MinValue;
            var windowEnd = MeetingBoundary(meeting.MeetingDate, meeting.MeetingTime);

            var betweenEvents = allCaseEvents
                .Where(cec => cec.BoardCaseId == row.boardCase.Id
                    && cec.CaseEvent.CreatedAt > windowStart
                    && cec.CaseEvent.CreatedAt <= windowEnd)
                .ToList();

            foreach (var cec in betweenEvents)
            {
                if (eventAttachmentsByCaseEventId.TryGetValue(cec.CaseEventId, out var atts))
                {
                    foreach (var att in atts)
                    {
                        referencedAttachments.TryAdd(att.FileName, att);
                        if (!attachmentLabelByFileName.ContainsKey(att.FileName))
                            attachmentLabelByFileName[att.FileName] = $"{agendaOrder}.{attachmentIndex}";
                        attachmentIndex++;
                    }
                }
            }
        }

        // Assemble per-item data
        var items = new List<AgendaItemData>();
        foreach (var row in agendaRows)
        {
            var assigneeName = userDisplay.TryGetValue(row.boardCase.AssigneeUserId ?? "", out var d) ? d : row.boardCase.AssigneeUserId;

            PreviousMinutesData? previousMinutes = null;
            if (previousByCaseId.TryGetValue(row.boardCase.Id, out var prev))
            {
                var prevAtts = prevMinutesAttachmentsByCaseEventId.TryGetValue(prev.CaseEventId, out var list) ? list : new List<AgendaAttachmentRef>();
                previousMinutes = new PreviousMinutesData(prev.MeetingDate, prev.MeetingTime, prev.OfficialNotes, prev.DecisionText, prev.FollowUpText, prevAtts);
            }

            var windowStart = previousMinutes is not null
                ? MeetingBoundary(previousMinutes.MeetingDate, previousMinutes.MeetingTime)
                : DateTimeOffset.MinValue;
            var windowEnd = MeetingBoundary(meeting.MeetingDate, meeting.MeetingTime);

            var between = allCaseEvents
                .Where(cec => cec.BoardCaseId == row.boardCase.Id
                    && cec.CaseEvent.CreatedAt > windowStart
                    && cec.CaseEvent.CreatedAt <= windowEnd)
                .OrderByDescending(cec => cec.CaseEvent.CreatedAt).ThenByDescending(cec => cec.CaseEventId)
                .Select(cec =>
                {
                    var atts = eventAttachmentsByCaseEventId.TryGetValue(cec.CaseEventId, out var al) ? al : new List<AgendaAttachmentRef>();
                    return new AgendaCommentData(cec.CaseEvent.CreatedAt.DateTime, cec.CaseEvent.Content, atts);
                })
                .ToList();

            items.Add(new AgendaItemData(row.mel, row.boardCase, assigneeName, previousMinutes, between));
        }

        return new AgendaPdfData(
            meeting,
            seq,
            items,
            attachmentLabelByFileName,
            referencedAttachments.Values.ToList());
    }
}
