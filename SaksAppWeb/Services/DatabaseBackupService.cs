using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace SaksAppWeb.Services;

public interface IDatabaseBackupExecutor
{
    Task CreateBackupAsync(CancellationToken ct = default);
}

public sealed class DatabaseBackupExecutor : IDatabaseBackupExecutor
{
    private const string NamePrefix = "app-";
    private const string StampFormat = "yyyyMMdd-HHmmss";

    /// <summary>
    /// Every format a backup has ever been written in. Retention must know about all of them —
    /// a glob that only matched ".sqlite" is why ~1 GB of ".bz2" was never reclaimed.
    /// </summary>
    private static readonly string[] BackupExtensions = [".sqlite", ".sqlite.gz", ".sqlite.bz2"];

    private readonly ILogger<DatabaseBackupExecutor> _logger;
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly int _maxFiles;
    private readonly long _maxTotalBytes;
    private readonly bool _compress;

    public DatabaseBackupExecutor(
        ILogger<DatabaseBackupExecutor> logger,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _logger = logger;

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var contentRoot = env.ContentRootPath;
        string dataSource = GetDataSourceFile(_connectionString) ?? "db/app.db";
        _dbPath = Path.IsPathRooted(dataSource) ? dataSource : Path.Combine(contentRoot, dataSource);
        _backupDir = Path.IsPathRooted(dataSource)
            ? Path.Combine(Path.GetDirectoryName(dataSource) ?? "", "Backups")
            : Path.Combine(contentRoot, "db/Backups");

        _maxFiles = Math.Max(1, configuration.GetValue<int?>("Backup:MaxFiles") ?? 48);
        _maxTotalBytes = Math.Max(1, configuration.GetValue<long?>("Backup:MaxTotalBytes") ?? 2L * 1024 * 1024 * 1024);
        _compress = configuration.GetValue<bool?>("Backup:Compress") ?? true;
    }

    public async Task CreateBackupAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_backupDir);

        if (!File.Exists(_dbPath))
        {
            _logger.LogWarning("Database file not found at {DbPath}. Skipping backup.", _dbPath);
            return;
        }

        // Prune before writing. On a full disk this is what frees the space the snapshot needs;
        // pruning only afterwards means the one failure that matters most is the one that skips it.
        ApplyRetention();

        // UtcNow, not Now: retention orders by the timestamp parsed out of the filename, so a
        // machine with a local time zone would emit non-monotonic names across a DST transition.
        var ts = DateTimeOffset.UtcNow;
        var stem = Path.Combine(_backupDir, $"{NamePrefix}{ts.ToString(StampFormat, CultureInfo.InvariantCulture)}.sqlite");
        var destPath = _compress ? stem + ".gz" : stem;
        var snapshotPath = stem + ".tmp";
        var stagedPath = destPath + ".part";

        try
        {
            // BackupDatabase needs a real SQLite file as its destination, so we always snapshot to
            // a temp file and only then produce the final artefact.
            await WriteSnapshotAsync(snapshotPath, ct);

            if (_compress)
            {
                await using (var input = File.OpenRead(snapshotPath))
                await using (var output = File.Create(stagedPath))
                await using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
                {
                    await input.CopyToAsync(gzip, ct);
                }

                // Move into place only once fully written: an interrupted run must never leave a
                // truncated file under a name that retention will treat as a good backup.
                File.Move(stagedPath, destPath, overwrite: true);
            }
            else
            {
                File.Move(snapshotPath, destPath, overwrite: true);
            }

            _logger.LogInformation("Database backup written to {BackupPath}", destPath);
        }
        finally
        {
            TryDelete(snapshotPath);
            TryDelete(stagedPath);
        }

        ApplyRetention();
    }

    private async Task WriteSnapshotAsync(string snapshotPath, CancellationToken ct)
    {
        var srcCs = new SqliteConnectionStringBuilder(_connectionString)
        {
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        // Pooling off so the file handle is released as soon as we dispose — we read it back
        // immediately to compress it.
        var destCs = new SqliteConnectionStringBuilder
        {
            DataSource = snapshotPath,
            Pooling = false
        }.ToString();

        await using var src = new SqliteConnection(srcCs);
        await src.OpenAsync(ct);

        await using var dest = new SqliteConnection(destCs);
        await dest.OpenAsync(ct);

        src.BackupDatabase(dest);
    }

    /// <summary>
    /// Bounded by file count AND total bytes, whichever binds first. The newest backup is never
    /// deleted. Ordering is by the timestamp in the filename: filesystem birth time is unreliable
    /// on Linux and can make the pass delete effectively arbitrary files.
    /// </summary>
    private void ApplyRetention()
    {
        try
        {
            var dir = new DirectoryInfo(_backupDir);
            if (!dir.Exists) return;

            var files = dir.EnumerateFiles(NamePrefix + "*.sqlite*")
                .Where(IsBackupFile)
                .Select(f => (Info: f, Stamp: ParseStamp(f.Name)))
                .OrderByDescending(x => x.Stamp)
                .ThenByDescending(x => x.Info.Name, StringComparer.Ordinal)
                .ToList();

            var kept = 0;
            var deleted = 0;
            long keptBytes = 0;
            long deletedBytes = 0;

            foreach (var (info, _) in files)
            {
                var isNewest = kept == 0;
                var overCount = kept >= _maxFiles;
                var overSize = keptBytes + info.Length > _maxTotalBytes;

                if (!isNewest && (overCount || overSize))
                {
                    var size = info.Length;
                    if (TryDelete(info.FullName))
                    {
                        deleted++;
                        deletedBytes += size;
                    }
                    continue;
                }

                kept++;
                keptBytes += info.Length;
            }

            _logger.LogInformation(
                "Backup retention: kept {Kept} file(s) ({KeptMb:F1} MB), deleted {Deleted} ({DeletedMb:F1} MB); limits {MaxFiles} files / {MaxMb:F0} MB",
                kept, keptBytes / 1048576d, deleted, deletedBytes / 1048576d, _maxFiles, _maxTotalBytes / 1048576d);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup retention pass failed");
        }
    }

    private static bool IsBackupFile(FileInfo f) =>
        BackupExtensions.Any(e => f.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>app-yyyyMMdd-HHmmss.sqlite[.gz|.bz2]. Unparseable names sort oldest so they are pruned first.</summary>
    public static DateTime ParseStamp(string fileName)
    {
        if (fileName.Length >= NamePrefix.Length + StampFormat.Length
            && DateTime.TryParseExact(
                fileName.Substring(NamePrefix.Length, StampFormat.Length),
                StampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }

    private bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {File}", path);
            return false;
        }
    }

    private static string? GetDataSourceFile(string connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (builder.DataSource is { Length: > 0 } ds)
                return ds;
        }
        catch { }
        return null;
    }
}

/// <summary>
/// Hosted service that runs DatabaseBackupExecutor on a periodic timer.
/// </summary>
public class DatabaseBackupService : BackgroundService
{
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly IDatabaseBackupExecutor _executor;
    private readonly TimeSpan _interval;

    public DatabaseBackupService(
        ILogger<DatabaseBackupService> logger,
        IDatabaseBackupExecutor executor,
        IConfiguration configuration)
    {
        _logger = logger;
        _executor = executor;

        var minutes = configuration.GetValue<int?>("Backup:IntervalMinutes") ?? 60;
        if (minutes < 1) minutes = 60;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch { }

        var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _executor.CreateBackupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database backup failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
