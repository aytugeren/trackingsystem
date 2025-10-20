using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace KuyumculukTakipProgrami.Infrastructure.Backup;

public class PgDumpRunner
{
    private readonly string _pgDumpPath;
    private readonly string _connectionString;
    private readonly bool _compress;

    public PgDumpRunner(string pgDumpPath, string connectionString, bool compress)
    {
        _pgDumpPath = pgDumpPath;
        _connectionString = connectionString;
        _compress = compress;
    }

    public async Task<(bool ok, string filePath, string? error)> RunDumpAsync(string outputDir, string filePrefix, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var ext = _compress ? ".sql.gz" : ".sql";
        var path = Path.Combine(outputDir, $"{filePrefix}_{timestamp}{ext}");

        // Build pg_dump args using connection string
        // Expected format: Host=...;Port=...;Database=...;Username=...;Password=...
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(_connectionString);

        var args = new StringBuilder();
        args.Append($"-h \"{builder.Host}\" -p {builder.Port} -U \"{builder.Username}\" -d \"{builder.Database}\" -F p --encoding=UTF8");

        var psi = new ProcessStartInfo
        {
            FileName = _pgDumpPath,
            Arguments = args.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Supply password via env var PGPASSWORD
        psi.Environment["PGPASSWORD"] = builder.Password;

        try
        {
            using var proc = new Process { StartInfo = psi };
            proc.Start();

            await using var outputStream = File.Create(path);
            if (_compress)
            {
                await using var gzip = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionLevel.SmallestSize);
                await proc.StandardOutput.BaseStream.CopyToAsync(gzip, ct);
            }
            else
            {
                await proc.StandardOutput.BaseStream.CopyToAsync(outputStream, ct);
            }

            var stdErr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                return (false, string.Empty, $"pg_dump exit {proc.ExitCode}: {stdErr}");
            }

            return (true, path, null);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            return (false, string.Empty, ex.Message);
        }
    }

    public static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(filePath);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

