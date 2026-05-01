using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;

namespace SaksAppWeb.Services;

public interface IMinutesSaveService
{
    /// <summary>Returns false if the meeting or minutes record was not found.</summary>
    Task<bool> SaveMinutesAsync(MeetingMinutesVm vm, CancellationToken ct = default);
}

public sealed class MinutesSaveService : IMinutesSaveService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public MinutesSaveService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<bool> SaveMinutesAsync(MeetingMinutesVm vm, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == vm.MeetingId, ct);
        if (meeting is null) return false;

        var minutes = await _db.MeetingMinutes.FirstOrDefaultAsync(x => x.MeetingId == vm.MeetingId, ct);
        if (minutes is null) return false;

        var beforeMinutes = new
        {
            minutes.AttendanceText,
            minutes.AbsenceText,
            minutes.ApprovalOfPreviousMinutesText,
            minutes.NextMeetingDate,
            minutes.EventueltText
        };

        minutes.AttendanceText = vm.AttendanceText;
        minutes.AbsenceText = vm.AbsenceText;
        minutes.ApprovalOfPreviousMinutesText = vm.ApprovalOfPreviousMinutesText;
        minutes.NextMeetingDate = vm.NextMeetingDate;
        minutes.EventueltText = vm.EventueltText;

        await _db.SaveChangesAsync(ct);

        var melIds = vm.CaseEntries.Select(e => e.MeetingEventLinkId).Where(x => x > 0).Distinct().ToList();
        var links = melIds.Count == 0
            ? new List<MeetingEventLink>()
            : await _db.MeetingEventLinks
                .Where(x => melIds.Contains(x.Id))
                .ToListAsync(ct);

        var linksById = links.ToDictionary(x => x.Id, x => x);

        foreach (var e in vm.CaseEntries)
        {
            if (!linksById.TryGetValue(e.MeetingEventLinkId, out var entity))
                continue;

            var beforeEntry = new { entity.OfficialNotes, entity.DecisionText, entity.FollowUpText, entity.Outcome };

            entity.OfficialNotes = e.OfficialNotes;
            entity.DecisionText = e.DecisionText;
            entity.FollowUpText = e.FollowUpText;
            entity.Outcome = e.Outcome;

            var afterEntry = new { entity.OfficialNotes, entity.DecisionText, entity.FollowUpText, entity.Outcome };

            await _audit.LogAsync(
                AuditAction.Update,
                nameof(MeetingEventLink),
                entity.Id.ToString(),
                beforeEntry,
                afterEntry,
                reason: $"Updated minutes entry for case #{e.CaseNumber}",
                ct: ct);
        }

        var afterMinutes = new
        {
            minutes.AttendanceText,
            minutes.AbsenceText,
            minutes.ApprovalOfPreviousMinutesText,
            minutes.NextMeetingDate,
            minutes.EventueltText
        };

        await _audit.LogAsync(
            AuditAction.Update,
            nameof(MeetingMinutes),
            minutes.Id.ToString(),
            beforeMinutes,
            afterMinutes,
            reason: "Updated minutes",
            ct: ct);

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
