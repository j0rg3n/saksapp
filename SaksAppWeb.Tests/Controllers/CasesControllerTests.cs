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

public class CasesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<ICaseNumberAllocator> _caseAllocatorMock;
    private readonly CasesController _controller;

    public CasesControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new ApplicationDbContext(options);
        
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        _auditMock = new Mock<IAuditService>();
        _caseAllocatorMock = new Mock<ICaseNumberAllocator>();
        
        _caseAllocatorMock.Setup(x => x.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _controller = new CasesController(
            _db,
            _userManagerMock.Object,
            _auditMock.Object,
            _caseAllocatorMock.Object,
            Mock.Of<ILogger<CasesController>>());

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
    public async Task Index_ReturnsOpenCasesByDefault()
    {
        // Arrange
        _db.BoardCases.Add(new BoardCase { CaseNumber = 1, Title = "Open Case", Status = CaseStatus.Open });
        _db.BoardCases.Add(new BoardCase { CaseNumber = 2, Title = "Closed Case", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Index(status: null, assigneeUserId: null, showClosed: false, CancellationToken.None);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Models.ViewModels.CaseIndexRowVm>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal("Open Case", model.First().Title);
    }

    [Fact]
    public async Task Index_WithShowClosed_ReturnsAllCases()
    {
        // Arrange
        _db.BoardCases.Add(new BoardCase { CaseNumber = 1, Title = "Open Case", Status = CaseStatus.Open });
        _db.BoardCases.Add(new BoardCase { CaseNumber = 2, Title = "Closed Case", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Index(status: null, assigneeUserId: null, showClosed: true, CancellationToken.None);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Models.ViewModels.CaseIndexRowVm>>(viewResult.Model);
        Assert.Equal(2, model.Count);
    }

    [Fact]
    public async Task Details_ReturnsCase_WhenExists()
    {
        // Arrange
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Details(boardCase.Id, CancellationToken.None);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.Model);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenNotExists()
    {
        // Act
        var result = await _controller.Details(999, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}