using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Infrastructure.Backup;
using KuyumculukTakipProgrami.Infrastructure.Optimization;

namespace KuyumculukTakipProgrami.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var marketConnection = configuration.GetConnectionString("MarketConnection") ?? connectionString;

        services.AddDbContext<KtpDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddDbContext<MarketDbContext>(options =>
        {
            options.UseNpgsql(marketConnection);
        });

        // CQRS Handlers
        services.AddScoped<ICreateInvoiceHandler, Handlers.Invoices.CreateInvoiceHandler>();
        services.AddScoped<IListInvoicesHandler, Handlers.Invoices.ListInvoicesHandler>();
        services.AddScoped<ICreateExpenseHandler, Handlers.Expenses.CreateExpenseHandler>();
        services.AddScoped<IListExpensesHandler, Handlers.Expenses.ListExpensesHandler>();

        // Background backup service (hourly backups + daily archive)
        services.AddHostedService<BackupBackgroundService>();
        // Enable index optimizer only if configured
        var enableIndex = configuration.GetSection("Optimization").GetValue<bool>("EnableIndexCreation", false);
        if (enableIndex)
        {
            services.AddHostedService<DbIndexOptimizer>();
        }

        return services;
    }
}
