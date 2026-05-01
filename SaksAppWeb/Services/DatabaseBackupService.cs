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
    private readonly ILogger<DatabaseBackupExecutor> _logger;
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly string _backupDir;

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
    }

    public async Task CreateBackupAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_backupDir);

        if (!File.Exists(_dbPath))
        {
            _logger.LogWarning("Database file not found at {DbPath}. Skipping backup.", _dbPath);
            return;
        }

        var ts = DateTimeOffset.Now;
        var destPath = Path.Combine(_backupDir, $"app-{ts:yyyyMMdd-HHmmss}.sqlite");

        var srcCs = new SqliteConnectionStringBuilder(_connectionString)
        {
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var src = new SqliteConnection(srcCs);
        await src.OpenAsync(ct);

        await using var dest = new SqliteConnection($"Data Source={destPath}");
        await dest.OpenAsync(ct);

        src.BackupDatabase(dest);

        _logger.LogInformation("Database backup written to {BackupPath}", destPath);

        TryApplyRetention(200);
    }

    private void TryApplyRetention(int maxFiles)
    {
        try
        {
            var files = new DirectoryInfo(_backupDir).GetFiles("app-*.sqlite");
            Array.Sort(files, (a, b) => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc));
            if (files.Length <= maxFiles) return;
            var toDelete = files.Length - maxFiles;
            for (int i = 0; i < toDelete; i++)
            {
                try { files[i].Delete(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete old backup {File}", files[i].FullName); }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup retention pass failed");
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
