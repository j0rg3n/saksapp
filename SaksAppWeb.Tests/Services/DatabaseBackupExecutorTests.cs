using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SaksAppWeb.Services;
using Xunit;

namespace SaksAppWeb.Tests.Services;

public class DatabaseBackupExecutorTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbPath;
    private readonly string _backupDir;

    public DatabaseBackupExecutorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"bak_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "app.db");
        _backupDir = Path.Combine(_root, "Backups");

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE Thing (Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO Thing (Name) VALUES ('x');";
        cmd.ExecuteNonQuery();
        SqliteConnection.ClearAllPools();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private DatabaseBackupExecutor CreateExecutor(
        int maxFiles = 48,
        long maxTotalBytes = 2L * 1024 * 1024 * 1024,
        bool compress = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["Backup:MaxFiles"] = maxFiles.ToString(),
                ["Backup:MaxTotalBytes"] = maxTotalBytes.ToString(),
                ["Backup:Compress"] = compress.ToString()
            })
            .Build();

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(_root);

        return new DatabaseBackupExecutor(NullLogger<DatabaseBackupExecutor>.Instance, config, env.Object);
    }

    private string[] BackupFiles() =>
        Directory.Exists(_backupDir)
            ? Directory.GetFiles(_backupDir).Select(Path.GetFileName).OfType<string>().OrderBy(x => x).ToArray()
            : [];

    /// <summary>Seeds an archive entry of a given size. Content is irrelevant to retention.</summary>
    private void Seed(string name, int bytes = 1024)
    {
        Directory.CreateDirectory(_backupDir);
        File.WriteAllBytes(Path.Combine(_backupDir, name), new byte[bytes]);
    }

    [Fact]
    public async Task CreateBackupAsync_WritesGzippedBackupThatOpensAsSqlite()
    {
        await CreateExecutor().CreateBackupAsync();

        var files = BackupFiles();
        var backup = Assert.Single(files);
        Assert.EndsWith(".sqlite.gz", backup);
        Assert.DoesNotContain(files, f => f.EndsWith(".tmp"));

        var restored = Path.Combine(_root, "restored.db");
        await using (var input = File.OpenRead(Path.Combine(_backupDir, backup)))
        await using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        await using (var output = File.Create(restored))
        {
            await gzip.CopyToAsync(output);
        }

        await using var conn = new SqliteConnection($"Data Source={restored};Mode=ReadOnly");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Thing";
        Assert.Equal("x", (string?)await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task CreateBackupAsync_WritesPlainFile_WhenCompressionDisabled()
    {
        await CreateExecutor(compress: false).CreateBackupAsync();

        var backup = Assert.Single(BackupFiles());
        Assert.EndsWith(".sqlite", backup);
        Assert.False(backup.EndsWith(".gz"));
    }

    [Fact]
    public async Task Retention_EnforcesFileCount_KeepingNewest()
    {
        Seed("app-20200101-000000.sqlite.gz");
        Seed("app-20200102-000000.sqlite.gz");
        Seed("app-20200103-000000.sqlite.gz");
        Seed("app-20200104-000000.sqlite.gz");

        await CreateExecutor(maxFiles: 3).CreateBackupAsync();

        var files = BackupFiles();
        Assert.Equal(3, files.Length);
        // The fresh backup plus the two most recent seeds survive; the oldest two are gone.
        Assert.Contains(files, f => f.StartsWith("app-20200104"));
        Assert.Contains(files, f => f.StartsWith("app-20200103"));
        Assert.DoesNotContain(files, f => f.StartsWith("app-20200102"));
        Assert.DoesNotContain(files, f => f.StartsWith("app-20200101"));
    }

    /// <summary>
    /// A ".sqlite"-only glob is why ~1 GB of ".bz2" was never reclaimed on the real archive.
    /// Retention must see every historical format.
    /// </summary>
    [Fact]
    public async Task Retention_SweepsLegacyPlainAndBz2Formats()
    {
        Seed("app-20200101-000000.sqlite.bz2");
        Seed("app-20200102-000000.sqlite");
        Seed("app-20200103-000000.sqlite.gz");

        await CreateExecutor(maxFiles: 1).CreateBackupAsync();

        var backup = Assert.Single(BackupFiles());
        Assert.EndsWith(".sqlite.gz", backup);
        Assert.StartsWith("app-20", backup);
        Assert.DoesNotContain("20200", backup);
    }

    [Fact]
    public async Task Retention_EnforcesTotalSizeBudget()
    {
        // Four 10 KB seeds; a 25 KB budget leaves room for the new backup plus a couple of seeds.
        for (var day = 1; day <= 4; day++)
            Seed($"app-202001{day:00}-000000.sqlite.gz", bytes: 10 * 1024);

        await CreateExecutor(maxTotalBytes: 25 * 1024).CreateBackupAsync();

        var total = Directory.GetFiles(_backupDir).Sum(f => new FileInfo(f).Length);
        Assert.True(total <= 25 * 1024, $"archive was {total} bytes, over the 25 KB budget");
        Assert.NotEmpty(BackupFiles());
    }

    [Fact]
    public async Task Retention_KeepsNewestEvenWhenItAloneExceedsBudget()
    {
        Seed("app-20200101-000000.sqlite.gz", bytes: 4096);

        await CreateExecutor(maxTotalBytes: 1).CreateBackupAsync();

        var backup = Assert.Single(BackupFiles());
        Assert.DoesNotContain("20200101", backup);
    }

    [Fact]
    public async Task CreateBackupAsync_SkipsWhenDatabaseMissing()
    {
        File.Delete(_dbPath);
        SqliteConnection.ClearAllPools();

        // Must not throw; nothing to back up.
        await CreateExecutor().CreateBackupAsync();

        Assert.Empty(BackupFiles());
    }

    [Theory]
    [InlineData("app-20260901-191631.sqlite", "2026-09-01T19:16:31")]
    [InlineData("app-20260901-191631.sqlite.gz", "2026-09-01T19:16:31")]
    [InlineData("app-20260316-110536.sqlite.bz2", "2026-03-16T11:05:36")]
    public void ParseStamp_ReadsTimestampFromFilename(string name, string expected)
    {
        Assert.Equal(DateTime.Parse(expected), DatabaseBackupExecutor.ParseStamp(name));
    }

    [Fact]
    public void ParseStamp_TreatsUnparseableNamesAsOldest()
    {
        Assert.Equal(DateTime.MinValue, DatabaseBackupExecutor.ParseStamp("app-notatimestamp.sqlite"));
    }
}
