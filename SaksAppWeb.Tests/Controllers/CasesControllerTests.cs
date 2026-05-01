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
using Microsoft.Extensions.Logging;
using SaksAppWeb.Models.ViewModels;

namespace SaksAppWeb.Tests.Controllers;

public class CasesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TestUserManager _userManager;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<ICaseNumberAllocator> _caseAllocatorMock;
    private readonly Mock<ICaseQueryService> _caseQueryMock;
    private readonly Mock<ILogger<CasesController>> _loggerMock;
    private readonly CasesController _controller;

    private readonly string _dbPath;

    public CasesControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        
        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();
        
        _userManager = new TestUserManager();
        
        _auditMock = new Mock<IAuditService>();
        _caseAllocatorMock = new Mock<ICaseNumberAllocator>();
        _caseQueryMock = new Mock<ICaseQueryService>();
        _loggerMock = new Mock<ILogger<CasesController>>();
        
        _caseAllocatorMock.Setup(x => x.AllocateNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _controller = new CasesController(
            _db,
            _userManager,
            _auditMock.Object,
            _caseAllocatorMock.Object,
            _caseQueryMock.Object,
            _loggerMock.Object);

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

    [Fact]
    public async Task Index_ReturnsOpenCasesByDefault()
    {
        var rows = new List<CaseIndexRowVm> { new() { Title = "Open Case", Status = CaseStatus.Open } };
        _caseQueryMock.Setup(x => x.GetFilteredCasesAsync(null, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await _controller.Index(status: null, assigneeUserId: null, showClosed: false, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<CaseIndexRowVm>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal("Open Case", model.First().Title);
    }

    [Fact]
    public async Task Index_WithShowClosed_ReturnsAllCases()
    {
        var rows = new List<CaseIndexRowVm>
        {
            new() { Title = "Open Case", Status = CaseStatus.Open },
            new() { Title = "Closed Case", Status = CaseStatus.Closed },
        };
        _caseQueryMock.Setup(x => x.GetFilteredCasesAsync(null, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await _controller.Index(status: null, assigneeUserId: null, showClosed: true, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<CaseIndexRowVm>>(viewResult.Model);
        Assert.Equal(2, model.Count);
    }

    [Fact]
    public async Task Create_Post_SavesNewCase()
    {
        var vm = new CaseEditVm
        {
            Title = "New Case",
            Description = "Description",
            Priority = CasePriority.P3,
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
    public async Task Details_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.Details(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

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

    [Fact]
    public async Task Edit_Get_ReturnsViewWithCase()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Edit Me", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var result = await _controller.Edit(boardCase.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<CaseEditVm>(viewResult.Model);
        Assert.Equal("Edit Me", vm.Title);
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenCaseNotExists()
    {
        var result = await _controller.Edit(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_UpdatesCase()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Old Title", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var vm = new CaseEditVm
        {
            Id = boardCase.Id,
            Title = "New Title",
            Priority = CasePriority.P1,
            Status = CaseStatus.Open
        };

        var result = await _controller.Edit(boardCase.Id, vm, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await _db.BoardCases.FindAsync(boardCase.Id);
        Assert.Equal("New Title", updated!.Title);
    }

    [Fact]
    public async Task Edit_Post_LogsStateChange_WhenStatusChanges()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "T", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var vm = new CaseEditVm { Id = boardCase.Id, Title = "T", Status = CaseStatus.Closed };

        await _controller.Edit(boardCase.Id, vm, CancellationToken.None);

        _auditMock.Verify(x => x.LogAsync(
            AuditAction.StateChange,
            nameof(BoardCase),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Edit_Post_ReturnsNotFound_WhenCaseNotExists()
    {
        var vm = new CaseEditVm { Id = 999, Title = "X", Status = CaseStatus.Open };

        var result = await _controller.Edit(999, vm, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SoftDeleteComment_SetsIsDeleted()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "T", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var comment = new CaseComment { BoardCaseId = boardCase.Id, Text = "Delete me", CreatedAt = DateTime.UtcNow };
        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync();

        var result = await _controller.SoftDeleteComment(comment.Id, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await _db.CaseComments.FindAsync(comment.Id);
        Assert.True(updated!.IsDeleted);
    }

    [Fact]
    public async Task SoftDeleteComment_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.SoftDeleteComment(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditComment_Get_ReturnsView()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "T", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var comment = new CaseComment { BoardCaseId = boardCase.Id, Text = "Existing text", CreatedAt = DateTime.UtcNow };
        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync();

        var result = await _controller.EditComment(comment.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<CommentEditVm>(viewResult.Model);
        Assert.Equal("Existing text", vm.Text);
    }

    [Fact]
    public async Task EditComment_Get_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.EditComment(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditComment_Post_UpdatesComment()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "T", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var comment = new CaseComment { BoardCaseId = boardCase.Id, Text = "Old text", CreatedAt = DateTime.UtcNow };
        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync();

        var vm = new CommentEditVm { Id = comment.Id, CaseId = boardCase.Id, Text = "Updated text" };

        var result = await _controller.EditComment(vm, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await _db.CaseComments.FindAsync(comment.Id);
        Assert.Equal("Updated text", updated!.Text);
    }

    [Fact]
    public async Task EditComment_Post_ReturnsNotFound_WhenNotExists()
    {
        var vm = new CommentEditVm { Id = 999, CaseId = 1, Text = "X" };

        var result = await _controller.EditComment(vm, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}