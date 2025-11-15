using System;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using PrintAgent.Logging;

namespace PrintAgent;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);
        var logFilePath = Path.Combine(logsDirectory, "printagent.log");

        var host = Host.CreateDefaultBuilder(args)
            .UseWindowsService() // sadece bu yeterli
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddProvider(new FileLoggerProvider(logFilePath, LogLevel.Debug));
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<AgentSettings>(context.Configuration.GetSection("Agent"));
                services.AddSingleton<PrintQueueRepository>();
                services.AddSingleton<ZebraPrinter>();
                services.AddHostedService<PrintAgentWorker>();
            })
            .Build();

        await host.RunAsync();
    }
}
