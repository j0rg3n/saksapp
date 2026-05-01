using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;

namespace SaksAppWeb.Services;

public interface ICaseQueryService
{
    Task<IReadOnlyList<CaseIndexRowVm>> GetFilteredCasesAsync(
        CaseStatus? status,
        string? assigneeUserId,
        bool showClosed,
        CancellationToken ct);

    Task<CaseDetailsVm?> GetCaseDetailsAsync(int id, CancellationToken ct);
}

public class CaseQueryService : ICaseQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserDisplayService _userDisplay;

    public CaseQueryService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserDisplayService userDisplay)
    {
        _db = db;
        _userManager = userManager;
        _userDisplay = userDisplay;
    }

    public async Task<IReadOnlyList<CaseIndexRowVm>> GetFilteredCasesAsync(
        CaseStatus? status,
        string? assigneeUserId,
        bool showClosed,
        CancellationToken ct)
    {
        var q = _db.BoardCases.AsNoTracking();

        if (status is not null)
            q = q.Where(x => x.Status == status);

        if (!showClosed)
            q = q.Where(x => x.Status != CaseStatus.Closed);

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

        var displayById = await _userDisplay.GetDisplayNamesAsync(assigneeIds, ct);

        return cases.Select(c => new CaseIndexRowVm
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            Title = c.Title,
            Priority = c.Priority,
            Status = c.Status,
            AssigneeUserId = c.AssigneeUserId,
            AssigneeDisplay = displayById.TryGetValue(c.AssigneeUserId ?? "", out var d) ? d : c.AssigneeUserId,
            CustomTidsfristDate = c.CustomTidsfristDate,
            CustomTidsfristText = c.CustomTidsfristText,
            Theme = c.Theme
        }).ToList();
    }

    public async Task<CaseDetailsVm?> GetCaseDetailsAsync(int id, CancellationToken ct)
    {
        var c = await _db.BoardCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        var comments = await _db.CaseComments.AsNoTracking()
            .Where(x => x.BoardCaseId == id)
            .ToListAsync(ct);

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

        var minutesEntryIds = minutesRows.Select(x => x.MinutesEntryId).Distinct().ToList();

        var minutesAtts = minutesEntryIds.Count == 0
            ? new List<(int EntryId, CaseAttachmentVm Att)>()
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
                .Select(x => new ValueTuple<int, CaseAttachmentVm>(
                    x.MeetingMinutesCaseEntryId,
                    new CaseAttachmentVm
                    {
                        LinkKind = CaseAttachmentLinkKind.MinutesEntryAttachment,
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

        var commentIds = comments.Select(x => x.Id).ToList();
        var commentAtts = commentIds.Count == 0
            ? new List<(int CommentId, CaseAttachmentVm Att)>()
            : await _db.CaseCommentAttachments.AsNoTracking()
                .Where(x => commentIds.Contains(x.CaseCommentId))
                .Join(_db.Attachments.AsNoTracking(),
                    link => link.AttachmentId,
                    att => att.Id,
                    (link, att) => new
                    {
                        link.CaseCommentId,
                        LinkId = link.Id,
                        AttachmentId = att.Id,
                        att.OriginalFileName,
                        att.ContentType,
                        att.SizeBytes
                    })
                .Select(x => new ValueTuple<int, CaseAttachmentVm>(
                    x.CaseCommentId,
                    new CaseAttachmentVm
                    {
                        LinkKind = CaseAttachmentLinkKind.CommentAttachment,
                        LinkId = x.LinkId,
                        AttachmentId = x.AttachmentId,
                        OriginalFileName = x.OriginalFileName,
                        ContentType = x.ContentType,
                        SizeBytes = x.SizeBytes
                    }))
                .ToListAsync(ct);

        var commentAttByCommentId = commentAtts
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToList());

        var userIds = comments
            .Select(x => x.CreatedByUserId)
            .Append(c.AssigneeUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var userDisplayById = await _userDisplay.GetDisplayNamesAsync(userIds, ct);

        var timeline = new List<CaseTimelineItemVm>();

        foreach (var com in comments)
        {
            timeline.Add(new CaseTimelineItemVm
            {
                Kind = CaseTimelineItemKind.Comment,
                OccurredAt = com.CreatedAt,
                SortId = com.Id,
                CommentId = com.Id,
                CommentText = com.Text,
                CommentAuthorUserId = com.CreatedByUserId,
                Attachments = commentAttByCommentId.TryGetValue(com.Id, out var cat) ? cat : new()
            });
        }

        foreach (var row in minutesRows.OrderBy(x => x.MeetingDate).ThenBy(x => x.YearSequenceNumber))
        {
            timeline.Add(new CaseTimelineItemVm
            {
                Kind = CaseTimelineItemKind.Minutes,
                OccurredAt = row.MeetingDate.ToDateTime(TimeOnly.MinValue),
                SortId = row.MinutesEntryId,
                MeetingMinutesCaseEntryId = row.MinutesEntryId,
                MeetingId = row.MeetingId,
                MeetingDate = row.MeetingDate,
                MeetingYear = row.Year,
                MeetingYearSequenceNumber = row.YearSequenceNumber,
                Outcome = row.Outcome,
                OfficialNotes = row.OfficialNotes,
                DecisionText = row.DecisionText,
                FollowUpText = row.FollowUpText,
                Attachments = minutesAttByEntryId.TryGetValue(row.MinutesEntryId, out var mat) ? mat : new()
            });
        }

        return new CaseDetailsVm
        {
            Case = c,
            UserDisplayById = userDisplayById,
            AssigneeDisplay = userDisplayById.TryGetValue(c.AssigneeUserId ?? "", out var ad) ? ad : c.AssigneeUserId,
            Timeline = timeline.OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.Kind)
                .ThenByDescending(x => x.SortId)
                .ToList()
        };
    }
}