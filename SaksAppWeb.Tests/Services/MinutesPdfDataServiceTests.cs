using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Services;

public class MinutesPdfDataServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TestUserManager _userManager;
    private readonly Mock<IPdfSequenceService> _pdfSequenceMock;
    private readonly MinutesPdfDataService _service;
    private readonly string _dbPath;

    public MinutesPdfDataServiceTests()
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

        _service = new MinutesPdfDataService(_db, _userManager, _pdfSequenceMock.Object);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetMinutesDataAsync_ReturnsNull_WhenMeetingNotFound()
    {
        var result = await _service.GetMinutesDataAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMinutesDataAsync_ReturnsNull_WhenMinutesRecordNotFound()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _service.GetMinutesDataAsync(meeting.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMinutesDataAsync_ReturnsEmptyEntries_WhenNoAgenda()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        _db.MeetingMinutes.Add(new MeetingMinutes { MeetingId = meeting.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetMinutesDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal(meeting.Id, result.Meeting.Id);
    }

    [Fact]
    public async Task GetMinutesDataAsync_ReturnsEntriesInAgendaOrder()
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
        _db.MeetingMinutes.Add(new MeetingMinutes { MeetingId = meeting.Id });
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce1.Id, BoardCaseId = c1.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce2.Id, BoardCaseId = c2.Id });
        _db.MeetingEventLinks.AddRange(
            new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce1.Id, AgendaOrder = 2, AgendaTextSnapshot = "" },
            new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce2.Id, AgendaOrder = 1, AgendaTextSnapshot = "" });
        await _db.SaveChangesAsync();

        var result = await _service.GetMinutesDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Second", result.Entries[0].Case.Title);
        Assert.Equal("First", result.Entries[1].Case.Title);
    }

    [Fact]
    public async Task GetMinutesDataAsync_AssignsSequentialAttachmentNumbers()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var ce = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(ce);
        _db.MeetingMinutes.Add(new MeetingMinutes { MeetingId = meeting.Id });
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var att1 = new Attachment { OriginalFileName = "a.pdf", ContentType = "application/pdf", SizeBytes = 10, Content = new byte[10], UploadedByUserId = "u" };
        var att2 = new Attachment { OriginalFileName = "b.pdf", ContentType = "application/pdf", SizeBytes = 10, Content = new byte[10], UploadedByUserId = "u" };
        _db.Attachments.AddRange(att1, att2);
        await _db.SaveChangesAsync();

        _db.CaseEventAttachments.AddRange(
            new CaseEventAttachment { CaseEventId = ce.Id, AttachmentId = att1.Id },
            new CaseEventAttachment { CaseEventId = ce.Id, AttachmentId = att2.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetMinutesDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Single(result.Entries);
        Assert.Equal(2, result.Entries[0].AttachmentNumbers.Count);
        Assert.Equal(1, result.Entries[0].AttachmentNumbers[0]);
        Assert.Equal(2, result.Entries[0].AttachmentNumbers[1]);
    }

    [Fact]
    public async Task GetMinutesDataAsync_IncludesSequenceNumber()
    {
        _pdfSequenceMock.Setup(x => x.AllocateNextAsync(It.IsAny<int>(), PdfDocumentType.Minutes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();
        _db.MeetingMinutes.Add(new MeetingMinutes { MeetingId = meeting.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetMinutesDataAsync(meeting.Id);

        Assert.NotNull(result);
        Assert.Equal(5, result.Sequence);
    }
}
