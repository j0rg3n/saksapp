using Xunit;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Tests.Services;

public class DataMigrationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly string _dbPath;

    public DataMigrationTests()
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
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private string MigrateMeetingCasesToCaseEventsSql => @"
        INSERT INTO CaseEvents (CreatedAt, Content, Category, IsDeleted, DeletedAt, DeletedByUserId)
        SELECT m.MeetingDate, '', 'meeting', mc.IsDeleted, mc.DeletedAt, mc.DeletedByUserId
        FROM MeetingCases mc
        JOIN Meetings m ON m.Id = mc.MeetingId
        ORDER BY mc.Id;

        WITH mcs AS (
            SELECT mc.Id AS mcId, mc.MeetingId, mc.BoardCaseId, mc.AgendaOrder, mc.AgendaTextSnapshot,
                   mc.TidsfristOverrideDate, mc.TidsfristOverrideText, mc.Outcome, mc.IsDeleted,
                   ROW_NUMBER() OVER (ORDER BY mc.Id) AS rn
            FROM MeetingCases mc
        ),
        ces AS (
            SELECT ce.Id AS ceId, ROW_NUMBER() OVER (ORDER BY ce.Id) AS rn
            FROM CaseEvents ce WHERE ce.Category = 'meeting'
        ),
        combined AS (
            SELECT mcs.MeetingId, ces.ceId AS CaseEventId, mcs.AgendaOrder, mcs.AgendaTextSnapshot,
                   mcs.TidsfristOverrideDate, mcs.TidsfristOverrideText, mcs.Outcome, mcs.IsDeleted,
                   mcs.mcId
            FROM mcs JOIN ces ON mcs.rn = ces.rn
        )
        INSERT INTO MeetingEventLinks (MeetingId, CaseEventId, AgendaOrder, AgendaTextSnapshot,
            TidsfristOverrideDate, TidsfristOverrideText, OfficialNotes, DecisionText, FollowUpText, Outcome, IsEventuelt, IsDeleted)
        SELECT c.MeetingId, c.CaseEventId, c.AgendaOrder, c.AgendaTextSnapshot,
               c.TidsfristOverrideDate, c.TidsfristOverrideText,
               mmce.OfficialNotes, mmce.DecisionText, mmce.FollowUpText,
               COALESCE(mmce.Outcome, c.Outcome),
               0,
               c.IsDeleted
        FROM combined c
        LEFT JOIN MeetingMinutesCaseEntries mmce ON mmce.MeetingCaseId = c.mcId AND NOT mmce.IsDeleted;

        WITH mcs AS (
            SELECT mc.BoardCaseId, mc.IsDeleted,
                   ROW_NUMBER() OVER (ORDER BY mc.Id) AS rn
            FROM MeetingCases mc
        ),
        ces AS (
            SELECT ce.Id AS ceId, ROW_NUMBER() OVER (ORDER BY ce.Id) AS rn
            FROM CaseEvents ce WHERE ce.Category = 'meeting'
        )
        INSERT INTO CaseEventCases (CaseEventId, BoardCaseId, IsDeleted)
        SELECT ces.ceId, mcs.BoardCaseId, mcs.IsDeleted
        FROM mcs JOIN ces ON mcs.rn = ces.rn;";

    private string MigrateCaseCommentsSql => @"
        INSERT INTO CaseEvents (CreatedAt, CreatedByUserId, Content, Category, IsDeleted, DeletedAt, DeletedByUserId)
        SELECT CreatedAt, CreatedByUserId, Text, 'comment', IsDeleted, DeletedAt, DeletedByUserId
        FROM CaseComments ORDER BY Id;

        WITH ccs AS (
            SELECT BoardCaseId, IsDeleted,
                   ROW_NUMBER() OVER (ORDER BY Id) AS rn
            FROM CaseComments
        ),
        ces AS (
            SELECT Id AS ceId, ROW_NUMBER() OVER (ORDER BY Id) AS rn
            FROM CaseEvents WHERE Category = 'comment'
        )
        INSERT INTO CaseEventCases (CaseEventId, BoardCaseId, IsDeleted)
        SELECT ces.ceId, ccs.BoardCaseId, ccs.IsDeleted
        FROM ccs JOIN ces ON ccs.rn = ces.rn;";

    [Fact]
    public async Task MigrateMeetingCases_CreatesCaseEventsAndLinks()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var mc = new MeetingCase { MeetingId = meeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1, AgendaTextSnapshot = "Snapshot" };
        _db.MeetingCases.Add(mc);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlRawAsync(MigrateMeetingCasesToCaseEventsSql);

        var events = await _db.Database.SqlQueryRaw<int>("SELECT Id FROM CaseEvents WHERE Category = 'meeting'").ToListAsync();
        var links = await _db.Database.SqlQueryRaw<int>("SELECT Id FROM MeetingEventLinks").ToListAsync();
        var cases = await _db.Database.SqlQueryRaw<int>("SELECT Id FROM CaseEventCases").ToListAsync();

        Assert.Single(events);
        Assert.Single(links);
        Assert.Single(cases);
    }

    [Fact]
    public async Task MigrateMeetingCases_PopulatesMinutesDataWhenPresent()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var mc = new MeetingCase { MeetingId = meeting.Id, BoardCaseId = boardCase.Id, AgendaOrder = 1, AgendaTextSnapshot = "" };
        _db.MeetingCases.Add(mc);
        await _db.SaveChangesAsync();

        _db.MeetingMinutesCaseEntries.Add(new MeetingMinutesCaseEntry
        {
            MeetingId = meeting.Id, MeetingCaseId = mc.Id, BoardCaseId = boardCase.Id,
            OfficialNotes = "Referat", DecisionText = "Vedtak", Outcome = MeetingCaseOutcome.Continue
        });
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlRawAsync(MigrateMeetingCasesToCaseEventsSql);

        var link = await _db.MeetingEventLinks.IgnoreQueryFilters().FirstAsync();
        Assert.Equal("Referat", link.OfficialNotes);
        Assert.Equal("Vedtak", link.DecisionText);
        Assert.Equal(MeetingCaseOutcome.Continue, link.Outcome);
    }

    [Fact]
    public async Task MigrateMeetingCases_HandlesMultipleCasesInCorrectOrder()
    {
        var meeting = new Meeting { MeetingDate = new DateOnly(2026, 3, 1), Year = 2026, YearSequenceNumber = 1 };
        _db.Meetings.Add(meeting);
        var c1 = new BoardCase { CaseNumber = 1, Title = "A", Status = CaseStatus.Open };
        var c2 = new BoardCase { CaseNumber = 2, Title = "B", Status = CaseStatus.Open };
        _db.BoardCases.AddRange(c1, c2);
        await _db.SaveChangesAsync();

        _db.MeetingCases.AddRange(
            new MeetingCase { MeetingId = meeting.Id, BoardCaseId = c1.Id, AgendaOrder = 1, AgendaTextSnapshot = "" },
            new MeetingCase { MeetingId = meeting.Id, BoardCaseId = c2.Id, AgendaOrder = 2, AgendaTextSnapshot = "" });
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlRawAsync(MigrateMeetingCasesToCaseEventsSql);

        var links = await _db.MeetingEventLinks.IgnoreQueryFilters().OrderBy(x => x.AgendaOrder).ToListAsync();
        var eventCases = await _db.CaseEventCases.IgnoreQueryFilters().ToListAsync();

        Assert.Equal(2, links.Count);
        Assert.Equal(2, eventCases.Count);
        Assert.Equal(1, links[0].AgendaOrder);
        Assert.Equal(2, links[1].AgendaOrder);
    }

    [Fact]
    public async Task MigrateCaseComments_CreatesCaseEventsAndLinks()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        _db.CaseComments.Add(new CaseComment
        {
            BoardCaseId = boardCase.Id,
            Text = "Hello world",
            CreatedAt = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero),
            CreatedByUserId = "user1"
        });
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlRawAsync(MigrateCaseCommentsSql);

        var events = await _db.CaseEvents.IgnoreQueryFilters().ToListAsync();
        var eventCases = await _db.CaseEventCases.IgnoreQueryFilters().ToListAsync();

        Assert.Single(events);
        Assert.Equal("Hello world", events[0].Content);
        Assert.Equal("comment", events[0].Category);
        Assert.Single(eventCases);
        Assert.Equal(boardCase.Id, eventCases[0].BoardCaseId);
    }

    [Fact]
    public async Task MigrateCaseComments_PreservesTextAndTimestamp()
    {
        var boardCase = new BoardCase { CaseNumber = 1, Title = "Test", Status = CaseStatus.Open };
        _db.BoardCases.Add(boardCase);
        await _db.SaveChangesAsync();

        var ts = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
        _db.CaseComments.Add(new CaseComment { BoardCaseId = boardCase.Id, Text = "Kommentar", CreatedAt = ts, CreatedByUserId = "u1" });
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlRawAsync(MigrateCaseCommentsSql);

        var ev = await _db.CaseEvents.IgnoreQueryFilters().FirstAsync();
        Assert.Equal("Kommentar", ev.Content);
        Assert.Equal(ts, ev.CreatedAt);
        Assert.Equal("u1", ev.CreatedByUserId);
    }
}
