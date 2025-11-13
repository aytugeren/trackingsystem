using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PrintAgent;

public sealed class PrintAgentWorker : BackgroundService
{
    private readonly PrintQueueRepository _repository;
    private readonly ZebraPrinter _printer;
    private readonly ILogger<PrintAgentWorker> _logger;
    private readonly AgentSettings _settings;

    public PrintAgentWorker(
        PrintQueueRepository repository,
        ZebraPrinter printer,
        IOptions<AgentSettings> options,
        ILogger<PrintAgentWorker> logger)
    {
        _repository = repository;
        _printer = printer;
        _logger = logger;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var machine = Environment.MachineName;
        _logger.LogInformation("PrintAgent started on machine {MachineName}.", machine);
        var delay = TimeSpan.FromMilliseconds(_settings.PollIntervalMs <= 0 ? 2000 : _settings.PollIntervalMs);

        await _repository.EnsureSchemaAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Checking queue...");
            try
            {
                var job = await _repository.GetNextJobAsync(stoppingToken);
                if (job is null)
                {
                    _logger.LogDebug("No pending jobs for {MachineName}. Sleeping for {Delay} ms.", machine, delay.TotalMilliseconds);
                }
                else
                {
                    _logger.LogInformation("Printing job {JobId}...", job.Id);
                    _printer.PrintZpl(job.Zpl ?? string.Empty);
                    await _repository.MarkAsPrintedAsync(job.Id, stoppingToken);
                    _logger.LogInformation("Printed job {JobId} successfully.", job.Id);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested, swallow.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process a print job. Retrying after delay.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
