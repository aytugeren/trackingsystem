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
                var connectionInfoStore = sp.GetRequiredService<TurmobConnectionInfoStore>();
                return new SocketsHttpHandler
                {
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                            var localEndpoint = socket.LocalEndPoint?.ToString();
                            var remoteEndpoint = socket.RemoteEndPoint?.ToString();
                            var host = context.DnsEndPoint?.Host;
                            var port = context.DnsEndPoint?.Port;
                            connectionInfoStore.Update(host, port, localEndpoint, remoteEndpoint);
                            if (optionsMonitor.CurrentValue?.Logging?.LogConnectionInfo == true)
                            {
                                logger.LogInformation(
                                    "TURMOB connection established. Host: {Host}, Port: {Port}, LocalEndpoint: {LocalEndpoint}, RemoteEndpoint: {RemoteEndpoint}",
                                    host ?? "unknown",
                                    port ?? 0,
                                    localEndpoint ?? "unknown",
                                    remoteEndpoint ?? "unknown");
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
        services.AddSingleton<TurmobConnectionInfoStore>();
        services.AddScoped<TurmobInvoiceBuilder>();
        services.AddScoped<ITurmobInvoiceGateway, TurmobInvoiceGateway>();

        return services;
    }
}
