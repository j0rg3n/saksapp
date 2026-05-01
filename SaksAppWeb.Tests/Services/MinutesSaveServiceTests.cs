using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Services;

public class MinutesSaveServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAuditService> _auditMock;
    private readonly MinutesSaveService _service;
    private readonly string _dbPath;

    public MinutesSaveServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();

        _auditMock = new Mock<IAuditService>();
        _service = new MinutesSaveService(_db, _auditMock.Object);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<(Meeting meeting, MeetingMinutes minutes, MeetingEventLink mel, int boardCaseId)> SeedAsync()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        var minutes = new MeetingMinutes { MeetingId = meeting.Id };
        _db.MeetingMinutes.Add(minutes);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        return (meeting, minutes, mel, boardCase.Id);
    }

    [Fact]
    public async Task SaveMinutesAsync_ReturnsFalse_WhenMeetingNotFound()
    {
        var vm = new MeetingMinutesVm { MeetingId = 999, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _service.SaveMinutesAsync(vm);

        Assert.False(result);
    }

    [Fact]
    public async Task SaveMinutesAsync_ReturnsFalse_WhenMinutesRecordNotFound()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var vm = new MeetingMinutesVm { MeetingId = meeting.Id, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _service.SaveMinutesAsync(vm);

        Assert.False(result);
    }

    [Fact]
    public async Task SaveMinutesAsync_UpdatesMinutesHeaderFields()
    {
        var (meeting, _, _, _) = await SeedAsync();

        var vm = new MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            AttendanceText = "Hansen, Johansen",
            AbsenceText = "Olsen",
            ApprovalOfPreviousMinutesText = "Godkjent",
            NextMeetingDate = new DateOnly(2026, 5, 1),
            EventueltText = "Ingen",
            CaseEntries = new List<MeetingMinutesCaseEntryVm>()
        };

        var result = await _service.SaveMinutesAsync(vm);

        Assert.True(result);
        var updated = await _db.MeetingMinutes.FirstAsync();
        Assert.Equal("Hansen, Johansen", updated.AttendanceText);
        Assert.Equal("Olsen", updated.AbsenceText);
        Assert.Equal("Godkjent", updated.ApprovalOfPreviousMinutesText);
        Assert.Equal(new DateOnly(2026, 5, 1), updated.NextMeetingDate);
        Assert.Equal("Ingen", updated.EventueltText);
    }

    [Fact]
    public async Task SaveMinutesAsync_UpdatesCaseEntryFields()
    {
        var (meeting, _, mel, boardCaseId) = await SeedAsync();

        var vm = new MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            CaseEntries = new List<MeetingMinutesCaseEntryVm>
            {
                new() { MeetingEventLinkId = mel.Id, BoardCaseId = boardCaseId, CaseNumber = 1,
                    OfficialNotes = "Diskutert", DecisionText = "Vedtatt", FollowUpText = "Sjekk neste møte",
                    Outcome = MeetingCaseOutcome.Continue }
            }
        };

        await _service.SaveMinutesAsync(vm);

        var updated = await _db.MeetingEventLinks.FirstAsync();
        Assert.Equal("Diskutert", updated.OfficialNotes);
        Assert.Equal("Vedtatt", updated.DecisionText);
        Assert.Equal("Sjekk neste møte", updated.FollowUpText);
        Assert.Equal(MeetingCaseOutcome.Continue, updated.Outcome);
    }

    [Fact]
    public async Task SaveMinutesAsync_CallsAuditLog_ForMinutesUpdate()
    {
        var (meeting, _, _, _) = await SeedAsync();

        var vm = new MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            AttendanceText = "Hansen",
            CaseEntries = new List<MeetingMinutesCaseEntryVm>()
        };

        await _service.SaveMinutesAsync(vm);

        _auditMock.Verify(x => x.LogAsync(
            AuditAction.Update,
            nameof(MeetingMinutes),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveMinutesAsync_CallsAuditLog_ForEachCaseEntry()
    {
        var (meeting, _, mel, boardCaseId) = await SeedAsync();

        var vm = new MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            CaseEntries = new List<MeetingMinutesCaseEntryVm>
            {
                new() { MeetingEventLinkId = mel.Id, BoardCaseId = boardCaseId, CaseNumber = 1, Outcome = MeetingCaseOutcome.Closed }
            }
        };

        await _service.SaveMinutesAsync(vm);

        _auditMock.Verify(x => x.LogAsync(
            AuditAction.Update,
            nameof(MeetingEventLink),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveMinutesAsync_ReturnsTrue_OnSuccess()
    {
        var (meeting, _, _, _) = await SeedAsync();

        var vm = new MeetingMinutesVm { MeetingId = meeting.Id, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _service.SaveMinutesAsync(vm);

        Assert.True(result);
    }
}
