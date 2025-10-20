using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KuyumculukTakipProgrami.Infrastructure.Backup;

public class BackupIndexStore
{
    private readonly string _indexFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BackupIndexStore(string indexFilePath)
    {
        _indexFilePath = indexFilePath;
    }

    public async Task<List<BackupRecord>> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_indexFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_indexFilePath)!);
                await File.WriteAllTextAsync(_indexFilePath, "[]", ct);
                return new List<BackupRecord>();
            }
            var json = await File.ReadAllTextAsync(_indexFilePath, ct);
            var list = JsonSerializer.Deserialize<List<BackupRecord>>(json, _jsonOptions) ?? new List<BackupRecord>();
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AppendAsync(BackupRecord record, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = await LoadUnlockedAsync(ct);
            list.Add(record);
            await SaveUnlockedAsync(list, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(List<BackupRecord> list, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await SaveUnlockedAsync(list, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<BackupRecord>> LoadUnlockedAsync(CancellationToken ct)
    {
        if (!File.Exists(_indexFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_indexFilePath)!);
            await File.WriteAllTextAsync(_indexFilePath, "[]", ct);
            return new List<BackupRecord>();
        }
        var json = await File.ReadAllTextAsync(_indexFilePath, ct);
        return JsonSerializer.Deserialize<List<BackupRecord>>(json, _jsonOptions) ?? new List<BackupRecord>();
    }

    private async Task SaveUnlockedAsync(List<BackupRecord> list, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_indexFilePath)!);
        var json = JsonSerializer.Serialize(list.OrderBy(x => x.CreatedAt).ToList(), _jsonOptions);
        await File.WriteAllTextAsync(_indexFilePath, json, ct);
    }
}

