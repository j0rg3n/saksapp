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

        _service = CreateService(TimeZoneInfo.Utc);
    }

    private sealed class FixedTimeZoneProvider(TimeZoneInfo tz) : IAppTimeZoneProvider
    {
        public TimeZoneInfo Local { get; } = tz;
    }

    private AgendaPdfDataService CreateService(TimeZoneInfo tz) =>
        new(_db, _userManager, _pdfSequenceMock.Object, new FixedTimeZoneProvider(tz));

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

        var ce1 = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        var ce2 = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.AddRange(ce1, ce2);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce1.Id, BoardCaseId = c1.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce2.Id, BoardCaseId = c2.Id });
        _db.MeetingEventLinks.AddRange(
            new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce1.Id, AgendaOrder = 2, AgendaTextSnapshot = "" },
            new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce2.Id, AgendaOrder = 1, AgendaTextSnapshot = "" });
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

        // Current meeting agenda
        var ceCurr = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        // Previous meeting event with notes
        var cePrev = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.AddRange(ceCurr, cePrev);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceCurr.Id, BoardCaseId = boardCase.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = cePrev.Id, BoardCaseId = boardCase.Id });
        _db.MeetingEventLinks.Add(new MeetingEventLink { MeetingId = currMeeting.Id, CaseEventId = ceCurr.Id, AgendaOrder = 1, AgendaTextSnapshot = "" });
        _db.MeetingEventLinks.Add(new MeetingEventLink
        {
            MeetingId = prevMeeting.Id, CaseEventId = cePrev.Id, AgendaOrder = 1, AgendaTextSnapshot = "",
            OfficialNotes = "Previous note", Outcome = MeetingCaseOutcome.Continue
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(currMeeting.Id);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.NotNull(result.Items[0].PreviousMinutes);
        Assert.Equal("Previous note", result.Items[0].PreviousMinutes!.OfficialNotes);
    }

    /// <summary>
    /// The agenda PDF must include every case-level category, not just the legacy "comment" value.
    /// It filtered on "comment" alone from Phase 3 until this was fixed, which silently dropped
    /// every event created after Phase 4.5 switched writes to general/avvik/tiltak.
    /// </summary>
    [Theory]
    [InlineData(CaseEventCategories.General)]
    [InlineData(CaseEventCategories.Avvik)]
    [InlineData(CaseEventCategories.Tiltak)]
    [InlineData(CaseEventCategories.LegacyComment)]
    public async Task GetAgendaDataAsync_IncludesAllCaseLevelCategories(string category)
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var ceMeeting = new CaseEvent { Category = CaseEventCategories.Meeting, Content = "", CreatedAt = DateTimeOffset.UtcNow };
        var ceHistory = new CaseEvent
        {
            Category = category,
            Content = "Hello",
            CreatedAt = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero)
        };
        _db.CaseEvents.AddRange(ceMeeting, ceHistory);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceMeeting.Id, BoardCaseId = boardCase.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceHistory.Id, BoardCaseId = boardCase.Id });
        _db.MeetingEventLinks.Add(new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ceMeeting.Id, AgendaOrder = 1, AgendaTextSnapshot = "" });
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Single(result.Items[0].CommentsBetweenMeetings);
        Assert.Equal("Hello", result.Items[0].CommentsBetweenMeetings[0].Text);
    }

    /// <summary>The PDF layer badges avvik/tiltak, so the category has to survive the projection.</summary>
    [Theory]
    [InlineData(CaseEventCategories.Avvik)]
    [InlineData(CaseEventCategories.Tiltak)]
    [InlineData(CaseEventCategories.General)]
    public async Task GetAgendaDataAsync_ExposesCategoryForBadging(string category)
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        await SeedHistoryEventAsync(meeting, new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero), category);

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(category, Assert.Single(result.Items[0].CommentsBetweenMeetings).Category);
    }

    /// <summary>
    /// MeetingTime is local wall-clock. In July Oslo is UTC+2, so an 18:00 meeting starts at
    /// 16:00 UTC and an event at 17:00 UTC happened *during* the meeting. The earlier UTC-naive
    /// boundary would have included it.
    /// </summary>
    [Theory]
    [InlineData(15, true)]
    [InlineData(17, false)]
    public async Task GetAgendaDataAsync_ResolvesMeetingTimeInLocalZone(int eventHourUtc, bool expectedIncluded)
    {
        var oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
        var meeting = new Meeting
        {
            MeetingDate = new DateOnly(2026, 7, 1),
            MeetingTime = new TimeOnly(18, 0),
            Year = 2026,
            YearSequenceNumber = 1
        };
        await SeedHistoryEventAsync(meeting, new DateTimeOffset(2026, 7, 1, eventHourUtc, 0, 0, TimeSpan.Zero));

        var result = await CreateService(oslo).GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(expectedIncluded ? 1 : 0, result.Items[0].CommentsBetweenMeetings.Count);
    }

    /// <summary>
    /// History dates are printed in the app time zone. 22:30 UTC on 30 September is 00:30 on
    /// 1 October in Oslo; printing the UTC value put the event on the wrong day.
    /// </summary>
    [Fact]
    public async Task GetAgendaDataAsync_ExposesHistoryTimestampInLocalZone()
    {
        var oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 11, 9), Year = 2026, YearSequenceNumber = 9 };
        await SeedHistoryEventAsync(meeting, new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero));

        var result = await CreateService(oslo).GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        var entry = Assert.Single(result.Items[0].CommentsBetweenMeetings);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 30, 0), entry.CreatedAtLocal);
    }

    [Fact]
    public async Task GetAgendaDataAsync_ExcludesMeetingCategoryFromHistory()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var ceMeeting = new CaseEvent { Category = CaseEventCategories.Meeting, Content = "", CreatedAt = DateTimeOffset.UtcNow };
        // A second meeting-category event dated inside the window: it is an agenda anchor, not history.
        var ceOtherMeeting = new CaseEvent
        {
            Category = CaseEventCategories.Meeting,
            Content = "Anchor from another meeting",
            CreatedAt = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero)
        };
        _db.CaseEvents.AddRange(ceMeeting, ceOtherMeeting);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceMeeting.Id, BoardCaseId = boardCase.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceOtherMeeting.Id, BoardCaseId = boardCase.Id });
        _db.MeetingEventLinks.Add(new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ceMeeting.Id, AgendaOrder = 1, AgendaTextSnapshot = "" });
        await _db.SaveChangesAsync();

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Empty(result.Items[0].CommentsBetweenMeetings);
    }

    [Theory]
    // Meeting starts 18:00. An event at 17:00 belongs to this meeting; 19:00 belongs to the next one.
    [InlineData(17, true)]
    [InlineData(19, false)]
    public async Task GetAgendaDataAsync_BoundsHistoryByMeetingTime(int eventHour, bool expectedIncluded)
    {
        var meeting = new Meeting
        {
            MeetingDate = new DateOnly(2026, 3, 1),
            MeetingTime = new TimeOnly(18, 0),
            Year = 2026,
            YearSequenceNumber = 1
        };
        await SeedHistoryEventAsync(meeting, new DateTimeOffset(2026, 3, 1, eventHour, 0, 0, TimeSpan.Zero));

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(expectedIncluded ? 1 : 0, result.Items[0].CommentsBetweenMeetings.Count);
    }

    /// <summary>
    /// With no time recorded the window runs to end of day, so a late event is still included.
    /// The old code anchored to 12:00 UTC and would have dropped this.
    /// </summary>
    [Fact]
    public async Task GetAgendaDataAsync_WithoutMeetingTime_IncludesEventLateOnMeetingDay()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        await SeedHistoryEventAsync(meeting, new DateTimeOffset(2026, 3, 1, 20, 0, 0, TimeSpan.Zero));

        var result = await _service.GetAgendaDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Single(result.Items[0].CommentsBetweenMeetings);
    }

    private async Task SeedHistoryEventAsync(
        Meeting meeting,
        DateTimeOffset eventCreatedAt,
        string category = CaseEventCategories.General)
    {
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var ceMeeting = new CaseEvent { Category = CaseEventCategories.Meeting, Content = "", CreatedAt = DateTimeOffset.UtcNow };
        var ceHistory = new CaseEvent { Category = category, Content = "Hello", CreatedAt = eventCreatedAt };
        _db.CaseEvents.AddRange(ceMeeting, ceHistory);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceMeeting.Id, BoardCaseId = boardCase.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ceHistory.Id, BoardCaseId = boardCase.Id });
        _db.MeetingEventLinks.Add(new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ceMeeting.Id, AgendaOrder = 1, AgendaTextSnapshot = "" });
        await _db.SaveChangesAsync();
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
