using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Application.Expenses;

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

        return services;
    }
}
