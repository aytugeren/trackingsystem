using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KuyumculukTakipProgrami.Api.Services;

public sealed class PrintQueueService : IPrintQueueService
{
    private readonly string _connectionString;
    private readonly ILogger<PrintQueueService> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaEnsured;

    public PrintQueueService(IConfiguration configuration, ILogger<PrintQueueService> logger)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:Default or ConnectionStrings:DefaultConnection is missing.");
        _logger = logger;
    }

    public async Task EnqueueAsync(IEnumerable<string> zpls, CancellationToken cancellationToken = default)
    {
        if (zpls is null) throw new ArgumentNullException(nameof(zpls));

        var payloads = new List<string>();
        foreach (var zpl in zpls)
        {
            if (!string.IsNullOrWhiteSpace(zpl))
            {
                payloads.Add(zpl);
            }
        }

        if (payloads.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);

        const string insertSql = @"INSERT INTO PrintQueue (Zpl) VALUES (@Zpl)";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(insertSql, payloads.Select(p => new { Zpl = p }));

        _logger.LogInformation("Enqueued {Count} labels into PrintQueue.", payloads.Count);
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaEnsured) return;

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaEnsured) return;

            const string createTableSql = @"
CREATE TABLE IF NOT EXISTS PrintQueue (
    Id SERIAL PRIMARY KEY,
    Zpl TEXT NOT NULL,
    IsPrinted BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PrintedAt TIMESTAMPTZ NULL,
    MachineName VARCHAR(50) NULL
);";

            const string addMachineNameColumnSql = @"
ALTER TABLE PrintQueue
ADD COLUMN IF NOT EXISTS MachineName VARCHAR(50) NULL;";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(createTableSql);
            await connection.ExecuteAsync(addMachineNameColumnSql);

            _schemaEnsured = true;
            _logger.LogInformation("PrintQueue schema was verified/created.");
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
