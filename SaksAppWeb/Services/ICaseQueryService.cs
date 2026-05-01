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

        // Load all CaseEventCases for this board case, including the CaseEvent and its MeetingLink (with Meeting)
        var caseEventCases = await _db.CaseEventCases.AsNoTracking()
            .Where(x => x.BoardCaseId == id)
            .Include(x => x.CaseEvent)
                .ThenInclude(ce => ce.MeetingLink)
                    .ThenInclude(mel => mel!.Meeting)
            .ToListAsync(ct);

        var caseEventIds = caseEventCases.Select(x => x.CaseEventId).Distinct().ToList();

        // Load attachments for all case events
        var eventAtts = caseEventIds.Count == 0
            ? new List<(int CaseEventId, int LinkId, int AttachmentId, string OriginalFileName, string ContentType, long SizeBytes)>()
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

        var eventAttsByCaseEventId = eventAtts
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.ToList());

        var userIds = caseEventCases
            .Where(x => x.CaseEvent.Category == "comment")
            .Select(x => x.CaseEvent.CreatedByUserId)
            .Append(c.AssigneeUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var userDisplayById = await _userDisplay.GetDisplayNamesAsync(userIds, ct);

        var timeline = new List<CaseTimelineItemVm>();

        foreach (var cec in caseEventCases)
        {
            var ce = cec.CaseEvent;
            var atts = eventAttsByCaseEventId.TryGetValue(ce.Id, out var rawAtts) ? rawAtts : new();

            if (ce.Category == "comment")
            {
                var attachments = atts.Select(a => new CaseAttachmentVm
                {
                    LinkKind = CaseAttachmentLinkKind.CommentAttachment,
                    LinkId = a.Item2,
                    AttachmentId = a.Item3,
                    OriginalFileName = a.Item4,
                    ContentType = a.Item5,
                    SizeBytes = a.Item6
                }).ToList();

                timeline.Add(new CaseTimelineItemVm
                {
                    Kind = CaseTimelineItemKind.Comment,
                    OccurredAt = ce.CreatedAt,
                    SortId = ce.Id,
                    CaseEventId = ce.Id,
                    CommentText = ce.Content,
                    CommentAuthorUserId = ce.CreatedByUserId,
                    Attachments = attachments
                });
            }
            else if (ce.Category == "meeting" && ce.MeetingLink is { } mel)
            {
                var attachments = atts.Select(a => new CaseAttachmentVm
                {
                    LinkKind = CaseAttachmentLinkKind.MinutesEntryAttachment,
                    LinkId = a.Item2,
                    AttachmentId = a.Item3,
                    OriginalFileName = a.Item4,
                    ContentType = a.Item5,
                    SizeBytes = a.Item6
                }).ToList();

                timeline.Add(new CaseTimelineItemVm
                {
                    Kind = CaseTimelineItemKind.Minutes,
                    OccurredAt = mel.Meeting.MeetingDate.ToDateTime(TimeOnly.MinValue),
                    SortId = mel.Id,
                    MeetingEventLinkId = mel.Id,
                    MeetingId = mel.MeetingId,
                    MeetingDate = mel.Meeting.MeetingDate,
                    MeetingYear = mel.Meeting.Year,
                    MeetingYearSequenceNumber = mel.Meeting.YearSequenceNumber,
                    Outcome = mel.Outcome,
                    OfficialNotes = mel.OfficialNotes,
                    DecisionText = mel.DecisionText,
                    FollowUpText = mel.FollowUpText,
                    Attachments = attachments
                });
            }
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