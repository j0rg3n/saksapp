using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public record MinutesAttachmentRef(string FileName, string ContentType, byte[] Content);

public record MinutesCaseEntryData(
    MeetingMinutesCaseEntry Entry,
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

        var rows = await _db.MeetingMinutesCaseEntries.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .Join(_db.MeetingCases.AsNoTracking(), e => e.MeetingCaseId, mc => mc.Id, (e, mc) => new { e, mc })
            .Join(_db.BoardCases.AsNoTracking(), x => x.e.BoardCaseId, c => c.Id, (x, c) => new { x.e, x.mc, c })
            .OrderBy(x => x.mc.AgendaOrder)
            .ToListAsync(ct);

        var assigneeIds = rows.Select(x => x.c.AssigneeUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var userDisplay = await _userManager.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email ?? x.UserName ?? x.Id, ct);

        var entryIds = rows.Select(x => x.e.Id).Distinct().ToList();
        var entryAttachmentsFull = entryIds.Count == 0
            ? new List<(int EntryId, string FileName, string ContentType, byte[] Content)>()
            : await _db.MeetingMinutesCaseEntryAttachments.AsNoTracking()
                .Where(x => entryIds.Contains(x.MeetingMinutesCaseEntryId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new { link.MeetingMinutesCaseEntryId, att.OriginalFileName, att.ContentType, att.Content })
                .Select(x => new ValueTuple<int, string, string, byte[]>(x.MeetingMinutesCaseEntryId, x.OriginalFileName, x.ContentType, x.Content))
                .ToListAsync(ct);

        var attachmentsByEntryId = entryAttachmentsFull
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => new MinutesAttachmentRef(x.Item2, x.Item3, x.Item4)).ToList());

        var seq = await _pdfSequence.AllocateNextAsync(meetingId, PdfDocumentType.Minutes, ct);

        // Assign global sequential attachment numbers
        var globalAttNum = 1;
        var entries = new List<MinutesCaseEntryData>();
        foreach (var row in rows)
        {
            var atts = attachmentsByEntryId.TryGetValue(row.e.Id, out var list) ? list : new List<MinutesAttachmentRef>();
            var nums = Enumerable.Range(globalAttNum, atts.Count).ToList();
            globalAttNum += atts.Count;

            var assigneeName = userDisplay.TryGetValue(row.c.AssigneeUserId ?? "", out var d) ? d : row.c.AssigneeUserId;
            entries.Add(new MinutesCaseEntryData(row.e, row.c, assigneeName, atts, nums));
        }

        var allAttachments = entryAttachmentsFull
            .Select(x => new MinutesAttachmentRef(x.Item2, x.Item3, x.Item4))
            .Distinct()
            .ToList();

        return new MinutesPdfData(meeting, minutes, seq, entries, allAttachments);
    }
}
