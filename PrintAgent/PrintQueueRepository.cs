using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PrintAgent.Models;

namespace PrintAgent;

    public sealed class PrintQueueRepository
{
    private readonly string _connectionString;
    private readonly string _machineName;
    private readonly ILogger<PrintQueueRepository> _logger;

    public PrintQueueRepository(
        IConfiguration configuration,
        IOptions<AgentSettings> options,
        ILogger<PrintQueueRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");
        _machineName = options.Value.MachineName ?? Environment.MachineName;
        _logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
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

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(createTableSql);
        await connection.ExecuteAsync(addMachineNameColumnSql);
        _logger.LogInformation("Ensured PrintQueue schema exists.");
    }

    public async Task<PrintJob?> GetNextJobAsync(CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, Zpl, IsPrinted, CreatedAt, PrintedAt, MachineName
FROM PrintQueue
WHERE IsPrinted = 0
  AND (MachineName IS NULL OR MachineName = @MachineName)
ORDER BY Id ASC
LIMIT 1;";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            return await connection.QueryFirstOrDefaultAsync<PrintJob>(sql, new { MachineName = _machineName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch next job from queue.");
            throw;
        }
    }

    public async Task MarkAsPrintedAsync(int jobId, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE PrintQueue
SET IsPrinted = 1, PrintedAt = NOW()
WHERE Id = @Id;";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { Id = jobId });
    }

    private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
