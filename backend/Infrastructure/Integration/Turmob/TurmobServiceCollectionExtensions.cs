using KuyumculukTakipProgrami.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public static class TurmobServiceCollectionExtensions
{
    public static IServiceCollection AddTurmobIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TurmobOptions>(configuration.GetSection(TurmobOptions.SectionName));
        services.AddHttpClient("TurmobSoap");
        services.AddSingleton<TurmobInvoiceMapper>();
        services.AddSingleton<TurmobSoapClient>();
        services.AddScoped<TurmobInvoiceBuilder>();
        services.AddScoped<ITurmobInvoiceGateway, TurmobInvoiceGateway>();

        return services;
    }
}
