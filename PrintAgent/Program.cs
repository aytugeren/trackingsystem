using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PrintAgent;

internal static class Program
{
    private static Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .UseConsoleLifetime()
            .ConfigureHostConfiguration(config => config.SetBasePath(AppContext.BaseDirectory))
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            // To convert this console agent into a Windows Service later, add .UseWindowsService() here.
            .ConfigureServices((context, services) =>
            {
                services.Configure<AgentSettings>(context.Configuration.GetSection("Agent"));
                services.AddSingleton<PrintQueueRepository>();
                services.AddSingleton<ZebraPrinter>();
                services.AddHostedService<PrintAgentWorker>();
            });

        return builder.RunConsoleAsync();
    }
}
