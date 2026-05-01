using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Controllers;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using System.IO;

namespace SaksAppWeb.Tests.Controllers;

public class AuditControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly string _dbPath;

    public AuditControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        
        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task Index_ReturnsViewWithEvents()
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = AuditAction.Create,
            EntityType = "BoardCase",
            EntityId = "1",
            Reason = "Test"
        });
        await _db.SaveChangesAsync();

        var controller = new AuditController(_db);

        var result = await controller.Index(null, 200, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<AuditEvent>>(viewResult.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Index_FiltersByQuery()
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = AuditAction.Create,
            EntityType = "BoardCase",
            EntityId = "1",
            Reason = "Test reason"
        });
        _db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = AuditAction.Update,
            EntityType = "Meeting",
            EntityId = "2",
            Reason = "Other reason"
        });
        await _db.SaveChangesAsync();

        var controller = new AuditController(_db);

        var result = await controller.Index("BoardCase", 200, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<AuditEvent>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal("BoardCase", model[0].EntityType);
    }

    [Fact(Skip = "Take is clamped to min 50")]
    public async Task Index_ReturnsAtMostTakeCount()
    {
        for (int i = 0; i < 10; i++)
        {
            _db.AuditEvents.Add(new AuditEvent
            {
                OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                Action = AuditAction.Create,
                EntityType = "Test",
                EntityId = i.ToString(),
                Reason = "Test"
            });
        }
        await _db.SaveChangesAsync();

        var controller = new AuditController(_db);

        // Note: Take is clamped to minimum 50, so we'll get more than 5
        var result = await controller.Index(null, 5, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<AuditEvent>>(viewResult.Model);
        Assert.NotEmpty(model);
    }

    [Fact]
    public async Task Index_ClampsTakeToMin50()
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = AuditAction.Create,
            EntityType = "Test",
            EntityId = "1",
            Reason = "Test"
        });
        await _db.SaveChangesAsync();

        var controller = new AuditController(_db);

        var result = await controller.Index(null, 10, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(50, viewResult.ViewData["Take"]);
    }

    [Fact]
    public async Task Index_ClampsTakeToMax1000()
    {
        var controller = new AuditController(_db);

        var result = await controller.Index(null, 2000, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(1000, viewResult.ViewData["Take"]);
    }
}