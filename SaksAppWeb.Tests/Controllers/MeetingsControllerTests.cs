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
using Microsoft.AspNetCore.Http;

namespace SaksAppWeb.Tests.Controllers;

public class MeetingsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IPdfSequenceService> _pdfSequenceMock;
    private readonly MeetingsController _controller;

    public MeetingsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new ApplicationDbContext(options);
        
        _auditMock = new Mock<IAuditService>();
        
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        _pdfSequenceMock = new Mock<IPdfSequenceService>();
        
        _controller = new MeetingsController(
            _db,
            _auditMock.Object,
            _userManagerMock.Object,
            _pdfSequenceMock.Object);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-123") };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task Index_ReturnsMeetingsOrderedByDate()
    {
        // Arrange
        _db.Meetings.Add(new Meeting { MeetingDate = new DateOnly(2026, 1, 1), Year = 2026, YearSequenceNumber = 1, Location = "Oslo" });
        _db.Meetings.Add(new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Bergen" });
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Index(CancellationToken.None);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IList<Meeting>>(viewResult.Model);
        Assert.Equal(2, model.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), model[0].MeetingDate);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenMeetingNotExists()
    {
        // Act
        var result = await _controller.Details(999, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsMeetingWithAgenda()
    {
        // Arrange
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 2, Location = "Oslo" };
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };
        
        _db.Meetings.Add(meeting);
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();
        
        var meetingCase = new MeetingCase { MeetingId = meeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1 };
        _db.MeetingCases.Add(meetingCase);
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Details(meeting.Id, CancellationToken.None);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.Model);
    }

    [Fact]
    public async Task CreateMeeting_SavesToDatabase()
    {
        // Arrange
        var vm = new Models.ViewModels.MeetingEditVm
        {
            MeetingDate = new DateOnly(2026, 4, 1),
            YearSequenceNumber = 3,
            Location = "Trondheim"
        };

        // Act
        var result = await _controller.Create(vm, CancellationToken.None);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirectResult.ActionName);
        
        var savedMeeting = await _db.Meetings.FirstOrDefaultAsync();
        Assert.NotNull(savedMeeting);
        Assert.Equal(new DateOnly(2026, 4, 1), savedMeeting.MeetingDate);
        Assert.Equal(2026, savedMeeting.Year);
        Assert.Equal(3, savedMeeting.YearSequenceNumber);
    }
}