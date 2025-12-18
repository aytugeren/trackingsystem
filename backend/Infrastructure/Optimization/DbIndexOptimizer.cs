using System;
using System.Threading;
using System.Threading.Tasks;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace KuyumculukTakipProgrami.Infrastructure.Optimization;

public class DbIndexOptimizer : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DbIndexOptimizer> _logger;
    private readonly IConfiguration _config;

    public DbIndexOptimizer(IServiceProvider sp, ILogger<DbIndexOptimizer> logger, IConfiguration config)
    {
        _sp = sp;
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Optional gate via config to avoid running in Production during cutover
            var enable = _config.GetSection("Optimization").GetValue<bool>("EnableIndexCreation", false);
            if (!enable)
            {
                _logger.LogInformation("DbIndexOptimizer disabled by config (Optimization:EnableIndexCreation=false)");
                return;
            }

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KtpDbContext>();

            // Speed up /api/invoices and /api/expenses listings by supporting ORDER BY + JOIN.
            var sql = new[]
            {
                // Invoices (use CONCURRENTLY to reduce locking on busy systems)
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Invoices_Kesildi_Tarih_SiraNo\" ON \"Invoices\" (\"Kesildi\" ASC, \"Tarih\" DESC, \"SiraNo\" DESC);",
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Invoices_KasiyerId\" ON \"Invoices\" (\"KasiyerId\");",
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Invoices_CustomerId\" ON \"Invoices\" (\"CustomerId\");",
                // Expenses
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Expenses_Kesildi_Tarih_SiraNo\" ON \"Expenses\" (\"Kesildi\" ASC, \"Tarih\" DESC, \"SiraNo\" DESC);",
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Expenses_KasiyerId\" ON \"Expenses\" (\"KasiyerId\");",
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Expenses_CustomerId\" ON \"Expenses\" (\"CustomerId\");",
                // Customers
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Customers_TCKN\" ON \"Customers\" (\"TCKN\");",
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Customers_NormalizedAdSoyad\" ON \"Customers\" (\"NormalizedAdSoyad\");"
            };

            foreach (var cmd in sql)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(cmd, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Index creation failed for command: {cmd}", cmd);
                }
            }

            // Optionally refresh planner stats for better plans after index creation
            try
            {
                await db.Database.ExecuteSqlRawAsync("ANALYZE \"Invoices\";", cancellationToken);
                await db.Database.ExecuteSqlRawAsync("ANALYZE \"Expenses\";", cancellationToken);
                await db.Database.ExecuteSqlRawAsync("ANALYZE \"Customers\";", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "ANALYZE failed (non-critical)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB optimization failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
