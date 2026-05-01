using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Controllers;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Services;
using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Http;
using SaksAppWeb.Models.ViewModels;
using SaksAppWeb.Tests;

namespace SaksAppWeb.Tests.Controllers;

public class MeetingsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAuditService> _auditMock;
    private readonly TestUserManager _userManager;
    private readonly Mock<IPdfSequenceService> _pdfSequenceMock;
    private readonly Mock<IMeetingQueryService> _meetingQueryMock;
    private readonly Mock<IAgendaPdfDataService> _agendaPdfDataMock;
    private readonly Mock<IMinutesPdfDataService> _minutesPdfDataMock;
    private readonly Mock<IMinutesSaveService> _minutesSaveMock;
    private readonly MeetingsController _controller;

    private readonly string _dbPath;

    public MeetingsControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();

        using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=OFF;";
        _db.Database.OpenConnection();
        cmd.ExecuteNonQuery();

        _auditMock = new Mock<IAuditService>();
        _userManager = new TestUserManager();
        _pdfSequenceMock = new Mock<IPdfSequenceService>();
        _meetingQueryMock = new Mock<IMeetingQueryService>();
        _agendaPdfDataMock = new Mock<IAgendaPdfDataService>();
        _minutesPdfDataMock = new Mock<IMinutesPdfDataService>();
        _minutesSaveMock = new Mock<IMinutesSaveService>();

        _controller = new MeetingsController(
            _db,
            _auditMock.Object,
            _userManager,
            _pdfSequenceMock.Object,
            _meetingQueryMock.Object,
            new Mock<ISimplePdfWriterFactory>().Object,
            _agendaPdfDataMock.Object,
            _minutesPdfDataMock.Object,
            _minutesSaveMock.Object);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-123") };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    #region Index Tests

    [Fact]
    public async Task Index_ReturnsMeetingsOrderedByDate()
    {
        var meetings = new List<Meeting>
        {
            new() { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Bergen" },
            new() { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" },
        };
        _meetingQueryMock.Setup(x => x.GetAllMeetingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(meetings);

        var result = await _controller.Index(CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Meeting>>(viewResult.Model);
        Assert.Equal(2, model.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), model[0].MeetingDate);
    }

    [Fact]
    public async Task Index_ReturnsEmptyList_WhenNoMeetings()
    {
        _meetingQueryMock.Setup(x => x.GetAllMeetingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _controller.Index(CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Meeting>>(viewResult.Model);
        Assert.Empty(model);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_Post_SavesNewMeeting()
    {
        var vm = new MeetingEditVm
        {
            MeetingDate = new DateOnly(2026, 4, 1),
            YearSequenceNumber = 3,
            Location = "Trondheim"
        };

        var result = await _controller.Create(vm, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirectResult.ActionName);
        
        var savedMeeting = await _db.Meetings.FirstOrDefaultAsync();
        Assert.NotNull(savedMeeting);
        Assert.Equal(new DateOnly(2026, 4, 1), savedMeeting.MeetingDate);
        Assert.Equal(2026, savedMeeting.Year);
    }

    [Fact]
    public async Task Create_Post_SetsYearFromDate()
    {
        var vm = new MeetingEditVm
        {
            MeetingDate = new DateOnly(2026, 6, 15),
            YearSequenceNumber = 1,
            Location = "Oslo"
        };

        await _controller.Create(vm, CancellationToken.None);

        var savedMeeting = await _db.Meetings.FirstOrDefaultAsync();
        Assert.Equal(2026, savedMeeting!.Year);
    }

    #endregion

    #region Details Tests

    [Fact]
    public async Task Details_ReturnsNotFound_WhenMeetingNotExists()
    {
        var result = await _controller.Details(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsMeetingWithAgenda()
    {
        var vm = new MeetingDetailsVm
        {
            Meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" },
            Agenda = Array.Empty<MeetingAgendaRowVm>(),
        };
        _meetingQueryMock.Setup(x => x.GetMeetingWithAgendaAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(vm);

        var result = await _controller.Details(42, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.Model);
    }

    #endregion

    #region Edit Tests

    [Fact]
    public async Task Edit_Get_ReturnsViewWithMeeting()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _controller.Edit(meeting.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<MeetingEditVm>(viewResult.Model);
        Assert.Equal("Oslo", vm.Location);
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenMeetingNotExists()
    {
        var result = await _controller.Edit(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_UpdatesMeeting()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var vm = new MeetingEditVm
        {
            Id = meeting.Id,
            MeetingDate = new DateOnly(2026, 5, 1),
            YearSequenceNumber = 5,
            Location = "Bergen"
        };

        var result = await _controller.Edit(vm, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        
        var updatedMeeting = await _db.Meetings.FindAsync(meeting.Id);
        Assert.Equal(new DateOnly(2026, 5, 1), updatedMeeting!.MeetingDate);
        Assert.Equal("Bergen", updatedMeeting.Location);
    }

    #endregion

    #region AddCase Tests

    [Fact]
    public async Task AddCase_AddsCaseToAgenda()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };

        _db.Meetings.Add(meeting);
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var result = await _controller.AddCase(meeting.Id, boardCase.Id, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);

        var mel = await _db.MeetingEventLinks.FirstOrDefaultAsync();
        Assert.NotNull(mel);
        Assert.Equal(meeting.Id, mel.MeetingId);

        var cec = await _db.CaseEventCases.FirstOrDefaultAsync();
        Assert.NotNull(cec);
        Assert.Equal(boardCase.Id, cec.BoardCaseId);
    }

    [Fact]
    public async Task AddCase_ReturnsNotFound_WhenMeetingNotExists()
    {
        var result = await _controller.AddCase(999, 1, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region EditAgendaItem Tests

    [Fact]
    public async Task EditAgendaItem_Post_UpdatesMeetingCase()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };

        _db.Meetings.Add(meeting);
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "Original" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var vm = new MeetingCaseEditVm
        {
            Id = mel.Id,
            AgendaTextSnapshot = "Updated agenda text",
            TidsfristOverrideDate = new DateOnly(2026, 6, 1)
        };

        var result = await _controller.EditAgendaItem(vm, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);

        var updated = await _db.MeetingEventLinks.FindAsync(mel.Id);
        Assert.Equal("Updated agenda text", updated!.AgendaTextSnapshot);
    }

    #endregion

    #region RemoveAgendaItem Tests

    [Fact]
    public async Task RemoveAgendaItem_SoftDeletes()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };

        _db.Meetings.Add(meeting);
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var result = await _controller.RemoveAgendaItem(mel.Id, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);

        var deleted = await _db.MeetingEventLinks.FindAsync(mel.Id);
        Assert.True(deleted!.IsDeleted);
    }

    #endregion

    #region MoveAgendaItem Tests

    [Fact]
    public async Task MoveAgendaItem_MovesUp()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        var boardCase1 = new BoardCase { CaseNumber = 1, Title = "Case 1", Status = CaseStatus.Open };
        var boardCase2 = new BoardCase { CaseNumber = 2, Title = "Case 2", Status = CaseStatus.Open };

        _db.Meetings.Add(meeting);
        _db.BoardCases.Add(boardCase1);
        _db.BoardCases.Add(boardCase2);
        await _db.SaveChangesAsync();

        var ce1 = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        var ce2 = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.AddRange(ce1, ce2);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce1.Id, BoardCaseId = boardCase1.Id });
        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = ce2.Id, BoardCaseId = boardCase2.Id });
        var mel1 = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce1.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        var mel2 = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = ce2.Id, AgendaOrder = 2, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.AddRange(mel1, mel2);
        await _db.SaveChangesAsync();

        var result = await _controller.MoveAgendaItem(mel2.Id, up: true, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);

        var updatedMel2 = await _db.MeetingEventLinks.FindAsync(mel2.Id);
        var updatedMel1 = await _db.MeetingEventLinks.FindAsync(mel1.Id);
        Assert.Equal(1, updatedMel2!.AgendaOrder);
        Assert.Equal(2, updatedMel1!.AgendaOrder);
    }

    #endregion

    #region Minutes Tests

    [Fact]
    public async Task Minutes_ReturnsNotFound_WhenMeetingNotExists()
    {
        var result = await _controller.Minutes(999, ct: CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Minutes_ReturnsMeetingWithCaseEntries()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var minutesVm = new MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            MeetingDate = meeting.MeetingDate,
            Year = meeting.Year,
            YearSequenceNumber = meeting.YearSequenceNumber,
            CaseEntries = new List<MeetingMinutesCaseEntryVm>
            {
                new() { MeetingEventLinkId = 1, BoardCaseId = 1, CaseNumber = 1, Title = "Test Case" }
            }
        };
        _meetingQueryMock.Setup(x => x.GetMeetingWithMinutesAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(minutesVm);

        var result = await _controller.Minutes(meeting.Id, ct: CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MeetingMinutesVm>(viewResult.Model);
        Assert.Single(model.CaseEntries);
        Assert.Equal(1, model.TotalCount);
        Assert.Equal(0, model.CurrentIndex);
    }

    [Fact]
    public async Task Minutes_Get_SetsCurrentIndexAndTotalCount()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var minutesVm = new MeetingMinutesVm
        {
            MeetingId = meeting.Id,
            MeetingDate = meeting.MeetingDate,
            Year = meeting.Year,
            YearSequenceNumber = meeting.YearSequenceNumber,
            CaseEntries = new List<MeetingMinutesCaseEntryVm>
            {
                new() { MeetingEventLinkId = 1, CaseNumber = 1, Title = "First" },
                new() { MeetingEventLinkId = 2, CaseNumber = 2, Title = "Second" },
                new() { MeetingEventLinkId = 3, CaseNumber = 3, Title = "Third" }
            }
        };
        _meetingQueryMock.Setup(x => x.GetMeetingWithMinutesAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(minutesVm);

        var result = await _controller.Minutes(meeting.Id, index: 1, ct: CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MeetingMinutesVm>(viewResult.Model);
        Assert.Single(model.CaseEntries);
        Assert.Equal(2, model.CaseEntries[0].CaseNumber);
        Assert.Equal(1, model.CurrentIndex);
        Assert.Equal(3, model.TotalCount);
    }

    [Fact]
    public async Task Minutes_Post_RedirectsOnSuccess()
    {
        _minutesSaveMock.Setup(x => x.SaveMinutesAsync(It.IsAny<MeetingMinutesVm>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var vm = new MeetingMinutesVm { MeetingId = 42, CurrentIndex = 0, TotalCount = 1, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _controller.Minutes(vm, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Minutes", redirect.ActionName);
        Assert.Equal(42, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Minutes_Post_NavigatesToNextOnActionNext()
    {
        _minutesSaveMock.Setup(x => x.SaveMinutesAsync(It.IsAny<MeetingMinutesVm>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var vm = new MeetingMinutesVm { MeetingId = 42, CurrentIndex = 0, TotalCount = 3, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _controller.Minutes(vm, "next", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Minutes", redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues!["index"]);
    }

    [Fact]
    public async Task Minutes_Post_RedirectsToDetailsWhenLastEntry()
    {
        _minutesSaveMock.Setup(x => x.SaveMinutesAsync(It.IsAny<MeetingMinutesVm>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var vm = new MeetingMinutesVm { MeetingId = 42, CurrentIndex = 2, TotalCount = 3, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _controller.Minutes(vm, "next", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
    }

    [Fact]
    public async Task Minutes_Post_ReturnsNotFound_WhenSaveReturnsFalse()
    {
        _minutesSaveMock.Setup(x => x.SaveMinutesAsync(It.IsAny<MeetingMinutesVm>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var vm = new MeetingMinutesVm { MeetingId = 999, CaseEntries = new List<MeetingMinutesCaseEntryVm>() };

        var result = await _controller.Minutes(vm, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Upload / Attachment Tests

    private static IFormFile MakePdfFile(string name = "test.pdf", long sizeBytes = 100)
    {
        var bytes = new byte[sizeBytes];
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(name);
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(sizeBytes);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, _) => s.Write(bytes));
        return file.Object;
    }

    private static IFormFile MakeImageFile(string name = "photo.jpg", long sizeBytes = 100)
    {
        var bytes = new byte[sizeBytes];
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(name);
        file.Setup(f => f.ContentType).Returns("image/jpeg");
        file.Setup(f => f.Length).Returns(sizeBytes);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, _) => s.Write(bytes));
        return file.Object;
    }

    [Fact]
    public async Task UploadSignedMinutes_StoresPdfAndCreatesLink()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _controller.UploadSignedMinutes(meeting.Id, MakePdfFile("minutes.pdf"), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await _db.Attachments.CountAsync());
        Assert.Equal(1, await _db.MeetingMinutesAttachments.CountAsync(x => x.MeetingId == meeting.Id));
    }

    [Fact]
    public async Task UploadSignedMinutes_ReturnsNotFound_WhenMeetingNotExists()
    {
        var result = await _controller.UploadSignedMinutes(999, MakePdfFile(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UploadSignedMinutes_RedirectsWhenFileIsEmpty()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _controller.UploadSignedMinutes(meeting.Id, null!, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Minutes", redirect.ActionName);
    }

    [Fact]
    public async Task UploadSignedMinutes_ReturnsBadRequest_WhenNotPdf()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _controller.UploadSignedMinutes(meeting.Id, MakeImageFile(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadAgendaAttachment_StoresAttachmentAndLink()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var result = await _controller.UploadAgendaAttachment(mel.Id, MakePdfFile("agenda.pdf"), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await _db.Attachments.CountAsync());
        Assert.Equal(1, await _db.CaseEventAttachments.CountAsync(x => x.CaseEventId == caseEvent.Id));
    }

    [Fact]
    public async Task UploadAgendaAttachment_ReturnsNotFound_WhenMeetingCaseNotExists()
    {
        var result = await _controller.UploadAgendaAttachment(999, MakePdfFile(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UploadAgendaAttachment_ReturnsBadRequest_WhenInvalidContentType()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var badFile = new Mock<IFormFile>();
        badFile.Setup(f => f.FileName).Returns("virus.exe");
        badFile.Setup(f => f.ContentType).Returns("application/x-msdownload");
        badFile.Setup(f => f.Length).Returns(100);

        var result = await _controller.UploadAgendaAttachment(mel.Id, badFile.Object, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadMinutesEntryAttachment_StoresAttachment()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var result = await _controller.UploadMinutesEntryAttachment(caseEvent.Id, MakePdfFile("note.pdf"), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await _db.Attachments.CountAsync());
        Assert.Equal(1, await _db.CaseEventAttachments.CountAsync(x => x.CaseEventId == caseEvent.Id));
    }

    [Fact]
    public async Task UploadMinutesEntryAttachment_ReturnsNotFound_WhenEntryNotExists()
    {
        var result = await _controller.UploadMinutesEntryAttachment(999, MakePdfFile(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveMinutesEntryAttachment_SoftDeletesLink()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var caseEvent = new CaseEvent { Category = "meeting", Content = "", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(caseEvent);
        var att = new Attachment { OriginalFileName = "f.pdf", ContentType = "application/pdf", SizeBytes = 10, Content = new byte[10], UploadedByUserId = "u" };
        _db.Attachments.Add(att);
        await _db.SaveChangesAsync();

        _db.CaseEventCases.Add(new CaseEventCase { CaseEventId = caseEvent.Id, BoardCaseId = boardCase.Id });
        var mel = new MeetingEventLink { MeetingId = meeting.Id, CaseEventId = caseEvent.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingEventLinks.Add(mel);
        await _db.SaveChangesAsync();

        var link = new CaseEventAttachment { CaseEventId = caseEvent.Id, AttachmentId = att.Id };
        _db.CaseEventAttachments.Add(link);
        await _db.SaveChangesAsync();

        var result = await _controller.RemoveMinutesEntryAttachment(link.Id, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await _db.CaseEventAttachments.FindAsync(link.Id);
        Assert.True(updated!.IsDeleted);
    }

    [Fact]
    public async Task RemoveMinutesEntryAttachment_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.RemoveMinutesEntryAttachment(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region PDF Download Tests

    [Fact]
    public async Task DownloadAgendaPdf_ReturnsNotFound_WhenDataServiceReturnsNull()
    {
        _agendaPdfDataMock.Setup(x => x.GetAgendaDataAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgendaPdfData?)null);

        var result = await _controller.DownloadAgendaPdf(42, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DownloadAgendaPdf_ReturnsFileResult_WhenDataExists()
    {
        var meeting = new Meeting { Id = 1, MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" };
        var data = new AgendaPdfData(
            meeting,
            Sequence: 1,
            Items: Array.Empty<AgendaItemData>(),
            AttachmentNumberByFileName: new Dictionary<string, int>(),
            AttachmentsInOrder: Array.Empty<AgendaAttachmentRef>());

        _agendaPdfDataMock.Setup(x => x.GetAgendaDataAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var capturingFactory = new CapturingPdfWriterFactory();
        var controller = new MeetingsController(
            _db, _auditMock.Object, _userManager, _pdfSequenceMock.Object,
            _meetingQueryMock.Object, capturingFactory,
            _agendaPdfDataMock.Object, _minutesPdfDataMock.Object, _minutesSaveMock.Object);
        controller.ControllerContext = _controller.ControllerContext;

        var result = await controller.DownloadAgendaPdf(1, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.NotNull(capturingFactory.Last);
        Assert.Contains(capturingFactory.Last!.Calls, c => c.StartsWith("Title:"));
    }

    [Fact]
    public async Task DownloadMinutesPdf_ReturnsNotFound_WhenDataServiceReturnsNull()
    {
        _minutesPdfDataMock.Setup(x => x.GetMinutesDataAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MinutesPdfData?)null);

        var result = await _controller.DownloadMinutesPdf(42, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DownloadMinutesPdf_ReturnsFileResult_WhenDataExists()
    {
        var meeting = new Meeting { Id = 2, MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        var minutes = new MeetingMinutes { Id = 1, MeetingId = 2 };
        var data = new MinutesPdfData(
            meeting, minutes,
            Sequence: 1,
            Entries: Array.Empty<MinutesCaseEntryData>(),
            AllAttachments: Array.Empty<MinutesAttachmentRef>());

        _minutesPdfDataMock.Setup(x => x.GetMinutesDataAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var capturingFactory = new CapturingPdfWriterFactory();
        var controller = new MeetingsController(
            _db, _auditMock.Object, _userManager, _pdfSequenceMock.Object,
            _meetingQueryMock.Object, capturingFactory,
            _agendaPdfDataMock.Object, _minutesPdfDataMock.Object, _minutesSaveMock.Object);
        controller.ControllerContext = _controller.ControllerContext;

        var result = await controller.DownloadMinutesPdf(2, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.NotNull(capturingFactory.Last);
        Assert.Contains(capturingFactory.Last!.Calls, c => c.StartsWith("Title:"));
    }

    #endregion
}