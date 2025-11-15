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
    private bool _schemaEnsured = false;

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

        bool schemaEnsured = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!schemaEnsured)
                {
                    await _repository.EnsureSchemaAsync(stoppingToken);
                    schemaEnsured = true;
                }

                var job = await _repository.GetNextJobAsync(stoppingToken);

                if (job != null)
                {
                    _printer.PrintZpl(job.Zpl ?? string.Empty);
                    await _repository.MarkAsPrintedAsync(job.Id, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
