using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Services;

public class AgendaPdfDataServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TestUserManager _userManager;
    private readonly Mock<IPdfSequenceService> _pdfSequenceMock;
    private readonly AgendaPdfDataService _service;
    private readonly string _dbPath;

    public AgendaPdfDataServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();

        _userManager = new TestUserManager();
        _pdfSequenceMock = new Mock<IPdfSequenceService>();
        _pdfSequenceMock.Setup(x => x.AllocateNextAsync(It.IsAny<int>(), It.IsAny<PdfDocumentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new AgendaPdfDataService(_db, _userManager, _pdfSequenceMock.Object);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetAgendaDataAsync_ReturnsNull_WhenMeetingNotFound()
    {
        var result = await _service.GetAgendaDataAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAgendaDataAsync_ReturnsEmptyItems_WhenNoAgenda()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(meeting.Id, result.Meeting.Id);
        Assert.Empty(result.Items);
        Assert.Equal(1, result.Sequence);
    }

    [Fact]
    public async Task GetAgendaDataAsync_ReturnsItemsInAgendaOrder()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var c1 = new BoardCase { CaseNumber = 1, Title = "First", Status = CaseStatus.Open };
        var c2 = new BoardCase { CaseNumber = 2, Title = "Second", Status = CaseStatus.Open };
        _db.BoardCases.AddRange(c1, c2);
        await _db.SaveChangesAsync();

        _db.MeetingCases.AddRange(
            new MeetingCase { MeetingId = meeting.Id, BoardCaseId = c1.Id, AgendaOrder = 2 },
            new MeetingCase { MeetingId = meeting.Id, BoardCaseId = c2.Id, AgendaOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Second", result.Items[0].Case.Title);
        Assert.Equal("First", result.Items[1].Case.Title);
    }

    [Fact]
    public async Task GetAgendaDataAsync_AssemblesPreviousMinutes()
    {
        var prevMeeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        var currMeeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2 };
        _db.Meetings.AddRange(prevMeeting, currMeeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var mc = new MeetingCase { MeetingId = currMeeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1 };
        _db.MeetingCases.Add(mc);
        var prevMc = new MeetingCase { MeetingId = prevMeeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1 };
        _db.MeetingCases.Add(prevMc);
        await _db.SaveChangesAsync();

        _db.MeetingMinutesCaseEntries.Add(new MeetingMinutesCaseEntry
        {
            MeetingId = prevMeeting.Id,
            MeetingCaseId = prevMc.Id,
            BoardCaseId = boardCase.Id,
            OfficialNotes = "Previous note",
            Outcome = MeetingCaseOutcome.Continue
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(currMeeting.Id);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.NotNull(result.Items[0].PreviousMinutes);
        Assert.Equal("Previous note", result.Items[0].PreviousMinutes!.OfficialNotes);
    }

    [Fact]
    public async Task GetAgendaDataAsync_AssemblesCommentsBetweenMeetings()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var mc = new MeetingCase { MeetingId = meeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1 };
        _db.MeetingCases.Add(mc);
        _db.CaseComments.Add(new CaseComment
        {
            BoardCaseId = boardCase.Id,
            Text = "Hello",
            CreatedAt = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero)
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Single(result.Items[0].CommentsBetweenMeetings);
        Assert.Equal("Hello", result.Items[0].CommentsBetweenMeetings[0].Text);
    }

    [Fact]
    public async Task GetAgendaDataAsync_IncludesSequenceNumber()
    {
        _pdfSequenceMock.Setup(x => x.AllocateNextAsync(It.IsAny<int>(), PdfDocumentType.Agenda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(7, result.Sequence);
    }
}
