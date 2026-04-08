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
using Microsoft.Extensions.Logging;

namespace SaksAppWeb.Tests.Controllers;

public class CasesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<ICaseNumberAllocator> _caseAllocatorMock;
    private readonly Mock<ILogger<CasesController>> _loggerMock;
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
        _loggerMock = new Mock<ILogger<CasesController>>();
        
        _caseAllocatorMock.Setup(x => x.AllocateNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _controller = new CasesController(
            _db,
            _userManagerMock.Object,
            _auditMock.Object,
            _caseAllocatorMock.Object,
            _loggerMock.Object);

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

    #region Index Tests

    [Fact]
    public async Task Index_ReturnsOpenCasesByDefault()
    {
        _db.BoardCases.Add(new BoardCase { CaseNumber = 1, Title = "Open Case", Status = CaseStatus.Open });
        _db.BoardCases.Add(new BoardCase { CaseNumber = 2, Title = "Closed Case", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await _controller.Index(status: null, assigneeUserId: null, showClosed: false, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Models.ViewModels.CaseIndexRowVm>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal("Open Case", model.First().Title);
    }

    [Fact]
    public async Task Index_WithShowClosed_ReturnsAllCases()
    {
        _db.BoardCases.Add(new BoardCase { CaseNumber = 1, Title = "Open Case", Status = CaseStatus.Open });
        _db.BoardCases.Add(new BoardCase { CaseNumber = 2, Title = "Closed Case", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await _controller.Index(status: null, assigneeUserId: null, showClosed: true, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Models.ViewModels.CaseIndexRowVm>>(viewResult.Model);
        Assert.Equal(2, model.Count);
    }

    [Fact]
    public async Task Index_FiltersByStatus()
    {
        _db.BoardCases.Add(new BoardCase { CaseNumber = 1, Title = "Open Case", Status = CaseStatus.Open });
        _db.BoardCases.Add(new BoardCase { CaseNumber = 2, Title = "Closed Case", Status = CaseStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await _controller.Index(status: CaseStatus.Closed, assigneeUserId: null, showClosed: false, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Models.ViewModels.CaseIndexRowVm>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal(CaseStatus.Closed, model.First().Status);
    }

    [Fact]
    public async Task Index_FiltersByAssignee()
    {
        _db.BoardCases.Add(new BoardCase { CaseNumber = 1, Title = "Case 1", Status = CaseStatus.Open, AssigneeUserId = "user-a" });
        _db.BoardCases.Add(new BoardCase { CaseNumber = 2, Title = "Case 2", Status = CaseStatus.Open, AssigneeUserId = "user-b" });
        await _db.SaveChangesAsync();

        var result = await _controller.Index(status: null, assigneeUserId: "user-a", showClosed: false, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<Models.ViewModels.CaseIndexRowVm>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal("user-a", model.First().AssigneeUserId);
    }

    #endregion

    #region Details Tests

    [Fact]
    public async Task Details_ReturnsCase_WhenExists()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var result = await _controller.Details(boardCase.Id, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.Details(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_IncludesComments()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test Case", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();
        
        _db.CaseComments.Add(new CaseComment { BoardCaseId = boardCase.Id, Text = "Test comment", CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _controller.Details(boardCase.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Models.ViewModels.CaseDetailsVm>(viewResult.Model);
        Assert.Single(model.Comments);
        Assert.Equal("Test comment", model.Comments.First().Text);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_Get_ReturnsViewWithVm()
    {
        var result = await _controller.Create(CancellationToken.None);
        
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.Model);
    }

    [Fact]
    public async Task Create_Post_SavesNewCase()
    {
        var vm = new Models.ViewModels.CaseEditVm
        {
            Title = "New Case",
            Description = "Description",
            Priority = CasePriority.High,
            Status = CaseStatus.Open,
            AssigneeUserId = "user-123"
        };

        var result = await _controller.Create(vm, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirectResult.ActionName);
        
        var savedCase = await _db.BoardCases.FirstOrDefaultAsync();
        Assert.NotNull(savedCase);
        Assert.Equal("New Case", savedCase.Title);
    }

    [Fact]
    public async Task Create_Post_LogsAudit()
    {
        var vm = new Models.ViewModels.CaseEditVm
        {
            Title = "New Case",
            Description = "Description",
            Priority = CasePriority.High,
            Status = CaseStatus.Open
        };

        await _controller.Create(vm, CancellationToken.None);

        _auditMock.Verify(x => x.LogAsync(
            AuditAction.Create,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Edit Tests

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenCaseNotExists()
    {
        var result = await _controller.Edit(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_UpdatesCase()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Original", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var vm = new Models.ViewModels.CaseEditVm
        {
            Id = boardCase.Id,
            Title = "Updated",
            Description = "New description",
            Priority = CasePriority.High,
            Status = CaseStatus.Open
        };

        var result = await _controller.Edit(boardCase.Id, vm, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        
        var updatedCase = await _db.BoardCases.FindAsync(boardCase.Id);
        Assert.Equal("Updated", updatedCase!.Title);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task SoftDelete_SetsIsDeleted()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "To Delete", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var result = await _controller.SoftDelete(boardCase.Id, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        
        var deletedCase = await _db.BoardCases.FindAsync(boardCase.Id);
        Assert.True(deletedCase!.IsDeleted);
    }

    #endregion

    #region AddComment Tests

    [Fact]
    public async Task AddComment_CreatesComment()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var result = await _controller.AddComment(boardCase.Id, "New comment", CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        
        var savedComment = await _db.CaseComments.FirstOrDefaultAsync();
        Assert.NotNull(savedComment);
        Assert.Equal("New comment", savedComment.Text);
    }

    #endregion

    #region EditComment Tests

    [Fact]
    public async Task EditComment_UpdatesText()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();
        
        var comment = new CaseComment { BoardCaseId = boardCase.Id, Text = "Original", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync();

        var vm = new Models.ViewModels.CommentEditVm { Id = comment.Id, Text = "Updated" };
        
        var result = await _controller.EditComment(vm, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        
        var updatedComment = await _db.CaseComments.FindAsync(comment.Id);
        Assert.Equal("Updated", updatedComment!.Text);
    }

    #endregion
}