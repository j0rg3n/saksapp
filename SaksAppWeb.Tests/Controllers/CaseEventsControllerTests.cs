using System.Security.Claims;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Controllers;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Controllers;

public class CaseEventsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<IUserDisplayService> _displayMock;
    private readonly CaseEventsController _controller;
    private readonly string _dbPath;

    public CaseEventsControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();

        _auditMock = new Mock<IAuditService>();
        _displayMock = new Mock<IUserDisplayService>();
        _displayMock.Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _controller = new CaseEventsController(_db, _auditMock.Object, _displayMock.Object);
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
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Index_ReturnsView_WithEditableCategories()
    {
        _db.CaseEvents.AddRange(
            new CaseEvent { Category = "avvik", Content = "Avvik 1", CreatedAt = DateTimeOffset.UtcNow },
            new CaseEvent { Category = "comment", Content = "Comment (excluded)", CreatedAt = DateTimeOffset.UtcNow },
            new CaseEvent { Category = "meeting", Content = "Meeting (excluded)", CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _controller.Index(null, CancellationToken.None) as ViewResult;

        var vm = Assert.IsType<CaseEventIndexVm>(result!.Model);
        Assert.Single(vm.Events);
        Assert.Equal("avvik", vm.Events[0].Category);
    }

    [Fact]
    public async Task Index_FiltersByCategory()
    {
        _db.CaseEvents.AddRange(
            new CaseEvent { Category = "avvik", Content = "Avvik", CreatedAt = DateTimeOffset.UtcNow },
            new CaseEvent { Category = "tiltak", Content = "Tiltak", CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _controller.Index("tiltak", CancellationToken.None) as ViewResult;

        var vm = Assert.IsType<CaseEventIndexVm>(result!.Model);
        Assert.Single(vm.Events);
        Assert.Equal("tiltak", vm.Events[0].Category);
    }

    [Fact]
    public void Create_Get_ReturnsView()
    {
        var result = _controller.Create(null) as ViewResult;

        var vm = Assert.IsType<CaseEventCreateVm>(result!.Model);
        Assert.Equal("avvik", vm.Category);
    }

    [Fact]
    public async Task Create_Post_SavesCaseEvent()
    {
        var vm = new CaseEventCreateVm { Category = "avvik", Content = "Test avvik" };

        var result = await _controller.Create(vm, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await _db.CaseEvents.IgnoreQueryFilters().FirstAsync();
        Assert.Equal("avvik", saved.Category);
        Assert.Equal("Test avvik", saved.Content);
    }

    [Fact]
    public async Task Create_Post_LinksCasesByNumber()
    {
        var boardCase = new BoardCase { CaseNumber = 42, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var vm = new CaseEventCreateVm { Category = "tiltak", Content = "Tiltak", CaseNumbers = "42" };

        await _controller.Create(vm, CancellationToken.None);

        var link = await _db.CaseEventCases.IgnoreQueryFilters().FirstAsync();
        Assert.Equal(boardCase.Id, link.BoardCaseId);
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.Edit(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_UpdatesContent()
    {
        var ev = new CaseEvent { Category = "avvik", Content = "Original", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(ev);
        await _db.SaveChangesAsync();

        var vm = new CaseEventEditVm { Id = ev.Id, Category = "tiltak", Content = "Updated" };
        var result = await _controller.Edit(vm, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await _db.CaseEvents.IgnoreQueryFilters().FirstAsync();
        Assert.Equal("tiltak", updated.Category);
        Assert.Equal("Updated", updated.Content);
    }

    [Fact]
    public async Task SoftDelete_SetsIsDeleted()
    {
        var ev = new CaseEvent { Category = "avvik", Content = "To delete", CreatedAt = DateTimeOffset.UtcNow };
        _db.CaseEvents.Add(ev);
        await _db.SaveChangesAsync();

        await _controller.SoftDelete(ev.Id, CancellationToken.None);

        var deleted = await _db.CaseEvents.IgnoreQueryFilters().FirstAsync();
        Assert.True(deleted.IsDeleted);
    }

    [Theory]
    [InlineData("1, 2, 3", new[] { 1, 2, 3 })]
    [InlineData("42", new[] { 42 })]
    [InlineData("", new int[] { })]
    [InlineData(null, new int[] { })]
    [InlineData("1;2 3", new[] { 1, 2, 3 })]
    public void ParseCaseNumbers_ParsesVariousFormats(string? input, int[] expected)
    {
        var result = CaseEventsController.ParseCaseNumbers(input);
        Assert.Equal(expected.OrderBy(x => x), result.OrderBy(x => x));
    }
}
