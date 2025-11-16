using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public sealed class GoldFeedBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GoldFeedBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public GoldFeedBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<GoldFeedBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = configuration.GetValue<int?>("Pricing:RefreshIntervalSeconds") ?? 30;
        if (seconds < 5) seconds = 5;
        _interval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gold pricing feed worker starting with {Interval} interval", _interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var refresher = scope.ServiceProvider.GetRequiredService<IGoldPricingRefreshService>();
                await refresher.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh gold pricing feed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        _logger.LogInformation("Gold pricing feed worker stopping");
    }
}
