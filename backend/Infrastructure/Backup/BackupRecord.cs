using System;

namespace KuyumculukTakipProgrami.Infrastructure.Backup;

public class BackupRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public string Type { get; set; } = "hourly"; // hourly|archive
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "success"; // success|failed
    public string? Message { get; set; }
    public bool Archived { get; set; }
    public string? ArchivePath { get; set; }
}

