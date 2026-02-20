using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace SaksAppWeb.Services
{
    /// <summary>
    /// Periodically creates a consistent backup of the SQLite database using the
    /// SQLite Online Backup API, safe while the DB is in use.
    /// </summary>
    public class DatabaseBackupService : BackgroundService
    {
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly string _backupDir;
        private readonly TimeSpan _interval;

        public DatabaseBackupService(
            ILogger<DatabaseBackupService> logger,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _logger = logger;

            // Use configured connection string and resolve DB path from it when it's a file-based SQLite.
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                  ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Resolve database file path; default to app.db in content root when relative.
            // This matches the provided connection string in appsettings.json (DataSource=app.db)
            var contentRoot = env.ContentRootPath;

            string dataSource = GetDataSourceFile(_connectionString) ?? "db/app.db";
            _dbPath = Path.IsPathRooted(dataSource) ? dataSource : Path.Combine(contentRoot, dataSource);

            // Backups directory under content root
            _backupDir = Path.IsPathRooted(dataSource) 
                ? Path.Combine(Path.GetDirectoryName(dataSource) ?? "", "Backups")
                : Path.Combine(contentRoot, "db/Backups");

            // Interval: default 1 hour, can be overridden via configuration: Backup:IntervalMinutes
            var minutes = configuration.GetValue<int?>("Backup:IntervalMinutes") ?? 60;
            if (minutes < 1) minutes = 60;
            _interval = TimeSpan.FromMinutes(minutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(_backupDir);

            // Small initial delay to avoid doing work right on startup time.
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch { }

            var timer = new PeriodicTimer(_interval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await CreateBackupAsync(stoppingToken);
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

        private async Task CreateBackupAsync(CancellationToken ct)
        {
            // Ensure DB exists
            if (!File.Exists(_dbPath))
            {
                _logger.LogWarning("Database file not found at {DbPath}. Skipping backup.", _dbPath);
                return;
            }

            var ts = DateTimeOffset.Now;
            var fileName = $"app-{ts:yyyyMMdd-HHmmss}.sqlite";
            var destPath = Path.Combine(_backupDir, fileName);

            // Use SQLite Online Backup API for a consistent backup while DB is in use.
            // Open source as read-only to avoid changing state.
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

            // Optional: simple retention - keep last 200 backups
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
                {
                    return ds;
                }
            }
            catch
            {
                // ignore parse errors; fall back to default
            }
            return null;
        }
    }
}
