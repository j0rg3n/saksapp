using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public record MinutesAttachmentRef(string FileName, string ContentType, byte[] Content);

public record MinutesCaseEntryData(
    MeetingEventLink Entry,
    BoardCase Case,
    string? AssigneeName,
    IReadOnlyList<MinutesAttachmentRef> Attachments,
    IReadOnlyList<int> AttachmentNumbers);

public record MinutesPdfData(
    Meeting Meeting,
    MeetingMinutes Minutes,
    int Sequence,
    IReadOnlyList<MinutesCaseEntryData> Entries,
    IReadOnlyList<MinutesAttachmentRef> AllAttachments);

public interface IMinutesPdfDataService
{
    Task<MinutesPdfData?> GetMinutesDataAsync(int meetingId, CancellationToken ct = default);
}

public sealed class MinutesPdfDataService : IMinutesPdfDataService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPdfSequenceService _pdfSequence;

    public MinutesPdfDataService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPdfSequenceService pdfSequence)
    {
        _db = db;
        _userManager = userManager;
        _pdfSequence = pdfSequence;
    }

    public async Task<MinutesPdfData?> GetMinutesDataAsync(int meetingId, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null) return null;

        var minutes = await _db.MeetingMinutes.AsNoTracking().FirstOrDefaultAsync(x => x.MeetingId == meetingId, ct);
        if (minutes is null) return null;

        // Load MeetingEventLinks for this meeting, including CaseEvent -> Cases -> BoardCase
        var links = await _db.MeetingEventLinks.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.Cases)
                    .ThenInclude(cec => cec.BoardCase)
            .OrderBy(x => x.AgendaOrder)
            .ToListAsync(ct);

        var rows = links
            .Select(mel =>
            {
                var cec = mel.CaseEvent.Cases.FirstOrDefault();
                return cec is null ? null : new { mel, boardCase = cec.BoardCase };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        var assigneeIds = rows.Select(x => x.boardCase.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var caseEventIds = rows.Select(x => x.mel.CaseEventId).Distinct().ToList();
        var entryAttachmentsFull = caseEventIds.Count == 0
            ? new List<(int CaseEventId, string FileName, string ContentType, byte[] Content)>()
            : await _db.CaseEventAttachments.AsNoTracking()
                .Where(x => caseEventIds.Contains(x.CaseEventId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new { link.CaseEventId, att.OriginalFileName, att.ContentType, att.Content })
                .Select(x => new ValueTuple<int, string, string, byte[]>(x.CaseEventId, x.OriginalFileName, x.ContentType, x.Content))
                .ToListAsync(ct);

        var attachmentsByCaseEventId = entryAttachmentsFull
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => new MinutesAttachmentRef(x.Item2, x.Item3, x.Item4)).ToList());

        var seq = await _pdfSequence.AllocateNextAsync(meetingId, PdfDocumentType.Minutes, ct);

        // Assign global sequential attachment numbers
        var globalAttNum = 1;
        var entries = new List<MinutesCaseEntryData>();
        foreach (var row in rows)
        {
            var atts = attachmentsByCaseEventId.TryGetValue(row.mel.CaseEventId, out var list) ? list : new List<MinutesAttachmentRef>();
            var nums = Enumerable.Range(globalAttNum, atts.Count).ToList();
            globalAttNum += atts.Count;

            var assigneeName = userDisplay.TryGetValue(row.boardCase.AssigneeUserId ?? "", out var d) ? d : row.boardCase.AssigneeUserId;
            entries.Add(new MinutesCaseEntryData(row.mel, row.boardCase, assigneeName, atts, nums));
        }

        var allAttachments = entryAttachmentsFull
            .Select(x => new MinutesAttachmentRef(x.Item2, x.Item3, x.Item4))
            .Distinct()
            .ToList();

        return new MinutesPdfData(meeting, minutes, seq, entries, allAttachments);
    }
}
