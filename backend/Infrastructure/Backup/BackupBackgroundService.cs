using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KuyumculukTakipProgrami.Infrastructure.Persistence;

namespace KuyumculukTakipProgrami.Infrastructure.Backup;

public class BackupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<BackupBackgroundService> _logger;
    private readonly BackupOptions _options;
    private readonly string _root;
    private readonly string _indexPath;
    private DateOnly? _lastArchivedDate;

    public BackupBackgroundService(IServiceProvider sp, ILogger<BackupBackgroundService> logger, IConfiguration config)
    {
        _sp = sp;
        _logger = logger;
        _options = config.GetSection("Backup").Get<BackupOptions>() ?? new BackupOptions();
        _root = Path.GetFullPath(_options.BackupRoot);
        _indexPath = Path.Combine(_root, "index.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Backup service disabled.");
            return;
        }

        Directory.CreateDirectory(_root);
        var archiveTime = ParseTime(_options.ArchiveAt);
        var index = new BackupIndexStore(_indexPath);

        _logger.LogInformation("Backup service started. Root: {root}", _root);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.Now;

                // Hourly backup on minute 0; if BusyStart/BusyEnd provided, skip when within busy window.
                if (now.Minute == 0 && IsAllowedNow(now, _options))
                {
                    await RunBackupAsync(index, stoppingToken);
                }

                // Daily archive at configured time
                if (now.Hour == archiveTime.hour && now.Minute == archiveTime.minute)
                {
                    var today = DateOnly.FromDateTime(now.Date);
                    if (_lastArchivedDate != today)
                    {
                        await RunArchiveAsync(index, today, stoppingToken);
                        await ApplyRetentionAsync(index, stoppingToken);
                        _lastArchivedDate = today;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private static bool IsAllowedNow(DateTimeOffset now, BackupOptions options)
    {
        // If busy window configured, disallow when within it.
        if (!string.IsNullOrWhiteSpace(options.BusyStart) && !string.IsNullOrWhiteSpace(options.BusyEnd))
        {
            var t = TimeOnly.FromDateTime(now.LocalDateTime);
            var (bsH, bsM) = ParseTime(options.BusyStart);
            var (beH, beM) = ParseTime(options.BusyEnd);
            var busyStart = new TimeOnly(bsH, bsM);
            var busyEnd = new TimeOnly(beH, beM);
            var inBusy = IsWithinWindow(t, busyStart, busyEnd);
            if (inBusy) return false;
            // Outside busy window: allowed.
            return true;
        }

        // Fallback to simple hour range
        return now.Hour >= options.HourlyStartHour && now.Hour <= options.HourlyEndHour;
    }

    private static bool IsWithinWindow(TimeOnly time, TimeOnly start, TimeOnly end)
    {
        // Handles windows that may span midnight.
        if (start <= end)
        {
            return time >= start && time <= end;
        }
        // e.g., 22:00 -> 06:00
        return time >= start || time <= end;
    }

    private async Task RunBackupAsync(BackupIndexStore index, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cs))
        {
            _logger.LogWarning("No connection string for backup");
            return;
        }

        var dayDir = Path.Combine(_root, "hourly", DateTimeOffset.Now.ToString("yyyy-MM-dd"));
        var runner = new PgDumpRunner(_options.PgDumpPath, cs, _options.Compress);
        var (ok, filePath, err) = await runner.RunDumpAsync(dayDir, "db_backup", ct);

        var rec = new BackupRecord
        {
            CreatedAt = DateTimeOffset.Now,
            Type = "hourly",
            FilePath = MakeRelative(filePath),
            Status = ok ? "success" : "failed",
            Message = ok ? null : err
        };

        if (ok)
        {
            try
            {
                var fi = new FileInfo(filePath);
                rec.SizeBytes = fi.Length;
                rec.ChecksumSha256 = PgDumpRunner.ComputeSha256(filePath);
                _logger.LogInformation("Backup created: {file} ({size} bytes)", filePath, fi.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute backup metadata");
            }
        }
        else
        {
            _logger.LogError("Backup failed: {error}", err);
        }

        await index.AppendAsync(rec, ct);
    }

    private async Task RunArchiveAsync(BackupIndexStore index, DateOnly date, CancellationToken ct)
    {
        var list = await index.LoadAsync(ct);
        var day = date.ToDateTime(TimeOnly.MinValue);
        var next = day.AddDays(1);
        var dayItems = list.Where(x => x.Type == "hourly" && x.CreatedAt >= day && x.CreatedAt < next && x.Status == "success" && !x.Archived).ToList();
        if (dayItems.Count == 0) return;

        var archiveDir = Path.Combine(_root, "archive", date.Year.ToString(), date.ToString("yyyy-MM"));
        Directory.CreateDirectory(archiveDir);
        var archivePath = Path.Combine(archiveDir, $"{date:yyyy-MM-dd}-backups.zip");

        try
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var item in dayItems)
                {
                    var absPath = Path.IsPathRooted(item.FilePath) ? item.FilePath : Path.Combine(_root, item.FilePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(absPath))
                    {
                        zip.CreateEntryFromFile(absPath, Path.GetFileName(absPath), CompressionLevel.SmallestSize);
                    }
                }
                // manifest
                var manifestEntry = zip.CreateEntry("manifest.txt");
                await using var writer = new StreamWriter(manifestEntry.Open());
                foreach (var item in dayItems)
                {
                    await writer.WriteLineAsync($"{item.CreatedAt:o}\t{Path.GetFileName(item.FilePath)}\t{item.SizeBytes}\t{item.ChecksumSha256}");
                }
            }

            foreach (var item in dayItems)
            {
                item.Archived = true;
                item.ArchivePath = MakeRelative(archivePath);
            }
            await index.SaveAsync(list, ct);
            _logger.LogInformation("Archived {count} backups to {archive}", dayItems.Count, archivePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Archive failed for {date}", date);
        }
    }

    private async Task ApplyRetentionAsync(BackupIndexStore index, CancellationToken ct)
    {
        if (_options.RetentionDays <= 0) return;
        var cutoff = DateTimeOffset.Now.AddDays(-_options.RetentionDays);
        var list = await index.LoadAsync(ct);
        var toDelete = list.Where(x => x.Type == "hourly" && x.Archived && x.CreatedAt < cutoff).ToList();
        foreach (var item in toDelete)
        {
            try
            {
                var abs = Path.IsPathRooted(item.FilePath) ? item.FilePath : Path.Combine(_root, item.FilePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(abs)) File.Delete(abs);
            }
            catch { }
        }
        // Keep index entries for history; optional: remove.
    }

    private static (int hour, int minute) ParseTime(string hhmm)
    {
        if (TimeOnly.TryParse(hhmm, out var t)) return (t.Hour, t.Minute);
        var parts = hhmm.Split(':');
        var h = parts.Length > 0 && int.TryParse(parts[0], out var hh) ? hh : 23;
        var m = parts.Length > 1 && int.TryParse(parts[1], out var mm) ? mm : 59;
        return (h, m);
    }

    private string MakeRelative(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var full = Path.GetFullPath(path);
        var rootFull = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return full.Substring(rootFull.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
        return full;
    }
}
