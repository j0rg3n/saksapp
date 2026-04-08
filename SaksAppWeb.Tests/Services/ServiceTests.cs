using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Services;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SaksAppWeb.Tests.Services;

public class PdfSequenceServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PdfSequenceService> _loggerMock;
    private readonly PdfSequenceService _service;

    public PdfSequenceServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new ApplicationDbContext(options);
        _loggerMock = Mock.Of<ILogger<PdfSequenceService>>();
        _service = new PdfSequenceService(_db, _loggerMock);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task AllocateNext_ReturnsSequenceNumber()
    {
        var result = await _service.AllocateNextAsync(1, PdfDocumentType.Agenda, CancellationToken.None);
        
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task AllocateNext_IncrementsForSameMeeting()
    {
        var first = await _service.AllocateNextAsync(1, PdfDocumentType.Agenda, CancellationToken.None);
        var second = await _service.AllocateNextAsync(1, PdfDocumentType.Agenda, CancellationToken.None);
        
        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task AllocateNext_SeparatesByDocumentType()
    {
        var agenda = await _service.AllocateNextAsync(1, PdfDocumentType.Agenda, CancellationToken.None);
        var minutes = await _service.AllocateNextAsync(1, PdfDocumentType.Minutes, CancellationToken.None);
        
        Assert.Equal(1, agenda);
        Assert.Equal(1, minutes);
    }

    [Fact]
    public async Task AllocateNext_DifferentMeetings_DifferentSequences()
    {
        var meeting1 = await _service.AllocateNextAsync(1, PdfDocumentType.Agenda, CancellationToken.None);
        var meeting2 = await _service.AllocateNextAsync(2, PdfDocumentType.Agenda, CancellationToken.None);
        
        Assert.Equal(1, meeting1);
        Assert.Equal(1, meeting2);
    }
}

public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SaksAppWeb.Services.AuditService> _loggerMock;
    private readonly SaksAppWeb.Services.AuditService _service;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new ApplicationDbContext(options);
        _loggerMock = Mock.Of<ILogger<SaksAppWeb.Services.AuditService>>();
        _service = new SaksAppWeb.Services.AuditService(_db, _loggerMock);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task LogAsync_CreatesAuditEvent()
    {
        await _service.LogAsync(
            AuditAction.Create,
            "BoardCase",
            "1",
            before: null,
            after: new { Title = "Test" },
            CancellationToken.None);

        var savedEvent = await _db.AuditEvents.FirstOrDefaultAsync();
        Assert.NotNull(savedEvent);
        Assert.Equal("BoardCase", savedEvent.EntityType);
        Assert.Equal("1", savedEvent.EntityId);
    }

    [Fact]
    public async Task LogAsync_CapturesAfterObject()
    {
        await _service.LogAsync(
            AuditAction.Create,
            "BoardCase",
            "1",
            before: null,
            after: new { Title = "New Case", Priority = 2 },
            CancellationToken.None);

        var savedEvent = await _db.AuditEvents.FirstOrDefaultAsync();
        Assert.Contains("New Case", savedEvent!.AfterJson);
    }

    [Fact]
    public async Task LogAsync_CapturesBeforeObject()
    {
        await _service.LogAsync(
            AuditAction.Update,
            "BoardCase",
            "1",
            before: new { Title = "Old Title" },
            after: new { Title = "New Title" },
            CancellationToken.None);

        var savedEvent = await _db.AuditEvents.FirstOrDefaultAsync();
        Assert.Contains("Old Title", savedEvent!.BeforeJson);
    }

    [Theory]
    [InlineData(AuditAction.Create)]
    [InlineData(AuditAction.Update)]
    [InlineData(AuditAction.Delete)]
    public async Task LogAsync_SupportsAllActionTypes(AuditAction action)
    {
        await _service.LogAsync(
            action,
            "TestEntity",
            "1",
            before: null,
            after: null,
            CancellationToken.None);

        var savedEvent = await _db.AuditEvents.FirstOrDefaultAsync();
        Assert.Equal(action, savedEvent!.Action);
    }
}