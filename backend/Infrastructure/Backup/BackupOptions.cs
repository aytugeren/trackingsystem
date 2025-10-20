using System;

namespace KuyumculukTakipProgrami.Infrastructure.Backup;

public class BackupOptions
{
    public bool Enabled { get; set; } = true;
    public string BackupRoot { get; set; } = "ops/backups";
    public string PgDumpPath { get; set; } = "pg_dump";
    public int HourlyStartHour { get; set; } = 8;  // inclusive
    public int HourlyEndHour { get; set; } = 20;   // inclusive
    public string ArchiveAt { get; set; } = "23:59"; // HH:mm (24h)
    public int RetentionDays { get; set; } = 30;
    public bool Compress { get; set; } = true;

    // Optional busy window to skip backups during heavy usage (HH:mm)
    public string? BusyStart { get; set; } // e.g. "09:00"
    public string? BusyEnd { get; set; }   // e.g. "19:30"
}
