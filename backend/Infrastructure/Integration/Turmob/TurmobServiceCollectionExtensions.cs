using System.Net;
using System.Net.Sockets;
using KuyumculukTakipProgrami.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public static class TurmobServiceCollectionExtensions
{
    public static IServiceCollection AddTurmobIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TurmobOptions>(configuration.GetSection(TurmobOptions.SectionName));
        services.AddHttpClient("TurmobSoap")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TurmobSoapClient>>();
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<TurmobOptions>>();
                return new SocketsHttpHandler
                {
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                            if (optionsMonitor.CurrentValue?.Logging?.LogConnectionInfo == true)
                            {
                                var localEndpoint = socket.LocalEndPoint?.ToString() ?? "unknown";
                                var remoteEndpoint = socket.RemoteEndPoint?.ToString() ?? "unknown";
                                var host = context.DnsEndPoint?.Host ?? "unknown";
                                var port = context.DnsEndPoint?.Port ?? 0;
                                logger.LogInformation(
                                    "TURMOB connection established. Host: {Host}, Port: {Port}, LocalEndpoint: {LocalEndpoint}, RemoteEndpoint: {RemoteEndpoint}",
                                    host,
                                    port,
                                    localEndpoint,
                                    remoteEndpoint);
                            }
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                };
            });
        services.AddSingleton<TurmobInvoiceMapper>();
        services.AddSingleton<TurmobSoapClient>();
        services.AddScoped<TurmobInvoiceBuilder>();
        services.AddScoped<ITurmobInvoiceGateway, TurmobInvoiceGateway>();

        return services;
    }
}
