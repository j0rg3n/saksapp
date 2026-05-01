using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Services;

public class CaseQueryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TestUserManager _userManager;
    private readonly Mock<IUserDisplayService> _userDisplayMock;
    private readonly CaseQueryService _service;
    private readonly string _dbPath;

    public CaseQueryServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();

        _userManager = new TestUserManager();
        _userDisplayMock = new Mock<IUserDisplayService>();
        _userDisplayMock
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _service = new CaseQueryService(_db, _userManager, _userDisplayMock.Object);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── GetFilteredCasesAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetFilteredCasesAsync_ExcludesClosedByDefault()
    {
        _db.BoardCases.AddRange(
            new BoardCase { CaseNumber = 1, Title = "Open", Status = CaseStatus.Open },
            new BoardCase { CaseNumber = 2, Title = "Closed", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await _service.GetFilteredCasesAsync(null, null, showClosed: false, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Open", result[0].Title);
    }

    [Fact]
    public async Task GetFilteredCasesAsync_IncludesClosedWhenShowClosedTrue()
    {
        _db.BoardCases.AddRange(
            new BoardCase { CaseNumber = 1, Title = "Open", Status = CaseStatus.Open },
            new BoardCase { CaseNumber = 2, Title = "Closed", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await _service.GetFilteredCasesAsync(null, null, showClosed: true, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetFilteredCasesAsync_FiltersByStatus()
    {
        _db.BoardCases.AddRange(
            new BoardCase { CaseNumber = 1, Title = "Open", Status = CaseStatus.Open },
            new BoardCase { CaseNumber = 2, Title = "Closed", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await _service.GetFilteredCasesAsync(CaseStatus.Closed, null, showClosed: true, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Closed", result[0].Title);
    }

    [Fact]
    public async Task GetFilteredCasesAsync_FiltersByAssignee()
    {
        _db.BoardCases.AddRange(
            new BoardCase { CaseNumber = 1, Title = "Assigned", Status = CaseStatus.Open, AssigneeUserId = "user-1" },
            new BoardCase { CaseNumber = 2, Title = "Unassigned", Status = CaseStatus.Open });
        await _db.SaveChangesAsync();

        var result = await _service.GetFilteredCasesAsync(null, "user-1", showClosed: false, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Assigned", result[0].Title);
    }

    [Fact]
    public async Task GetFilteredCasesAsync_ReturnsOrderedByDescendingCaseNumber()
    {
        _db.BoardCases.AddRange(
            new BoardCase { CaseNumber = 1, Title = "First", Status = CaseStatus.Open },
            new BoardCase { CaseNumber = 5, Title = "Fifth", Status = CaseStatus.Open },
            new BoardCase { CaseNumber = 3, Title = "Third", Status = CaseStatus.Open });
        await _db.SaveChangesAsync();

        var result = await _service.GetFilteredCasesAsync(null, null, showClosed: false, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(5, result[0].CaseNumber);
        Assert.Equal(3, result[1].CaseNumber);
        Assert.Equal(1, result[2].CaseNumber);
    }

    [Fact]
    public async Task GetFilteredCasesAsync_ReturnsEmptyWhenNoCases()
    {
        var result = await _service.GetFilteredCasesAsync(null, null, showClosed: false, CancellationToken.None);

        Assert.Empty(result);
    }

    // ── GetCaseDetailsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetCaseDetailsAsync_ReturnsNullWhenCaseNotFound()
    {
        var result = await _service.GetCaseDetailsAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCaseDetailsAsync_ReturnsCaseWithEmptyTimeline()
    {
        var c = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(c);
        await _db.SaveChangesAsync();

        var result = await _service.GetCaseDetailsAsync(c.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(c.Id, result.Case.Id);
        Assert.Empty(result.Timeline);
    }

    [Fact]
    public async Task GetCaseDetailsAsync_IncludesCommentsInTimeline()
    {
        var c = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(c);
        await _db.SaveChangesAsync();

        _db.CaseComments.Add(new CaseComment
        {
            BoardCaseId = c.Id,
            Text = "First comment",
            CreatedAt = new DateTime(2026, 1, 1)
        });
        _db.CaseComments.Add(new CaseComment
        {
            BoardCaseId = c.Id,
            Text = "Second comment",
            CreatedAt = new DateTime(2026, 2, 1)
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetCaseDetailsAsync(c.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Timeline.Count);
        Assert.All(result.Timeline, t => Assert.Equal(CaseTimelineItemKind.Comment, t.Kind));
    }

    [Fact]
    public async Task GetCaseDetailsAsync_IncludesMinutesEntriesInTimeline()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var mc = new MeetingCase { MeetingId = meeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1 };
        _db.MeetingCases.Add(mc);
        await _db.SaveChangesAsync();

        _db.MeetingMinutesCaseEntries.Add(new MeetingMinutesCaseEntry
        {
            MeetingId = meeting.Id,
            MeetingCaseId = mc.Id,
            BoardCaseId = boardCase.Id,
            Outcome = MeetingCaseOutcome.Continue,
            OfficialNotes = "Discussed"
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetCaseDetailsAsync(boardCase.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Timeline);
        Assert.Equal(CaseTimelineItemKind.Minutes, result.Timeline[0].Kind);
        Assert.Equal("Discussed", result.Timeline[0].OfficialNotes);
    }

    [Fact]
    public async Task GetCaseDetailsAsync_MixedTimeline_OrderedByDateDescending()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 10), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var mc = new MeetingCase { MeetingId = meeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1 };
        _db.MeetingCases.Add(mc);
        await _db.SaveChangesAsync();

        _db.MeetingMinutesCaseEntries.Add(new MeetingMinutesCaseEntry
        {
            MeetingId = meeting.Id, MeetingCaseId = mc.Id, BoardCaseId = boardCase.Id,
            Outcome = MeetingCaseOutcome.Continue
        });
        _db.CaseComments.Add(new CaseComment
        {
            BoardCaseId = boardCase.Id, Text = "Later comment",
            CreatedAt = new DateTime(2026, 3, 1)
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetCaseDetailsAsync(boardCase.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Timeline.Count);
        // Comment from March comes first (descending order)
        Assert.Equal(CaseTimelineItemKind.Comment, result.Timeline[0].Kind);
        Assert.Equal(CaseTimelineItemKind.Minutes, result.Timeline[1].Kind);
    }
}
