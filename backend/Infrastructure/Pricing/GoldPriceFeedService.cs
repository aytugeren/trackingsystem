using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public class GoldPriceFeedService : BackgroundService
{
    static GoldPriceFeedService()
    {
        // Enable legacy encodings (e.g., windows-1254) for external feeds.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly IServiceProvider _sp;
    private readonly ILogger<GoldPriceFeedService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoldPriceFeedOptions _options;

    public GoldPriceFeedService(
        IServiceProvider sp,
        ILogger<GoldPriceFeedService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _sp = sp;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = configuration.GetSection("PricingFeed").Get<GoldPriceFeedOptions>() ?? new GoldPriceFeedOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.BreakOnStart && !Debugger.IsAttached)
        {
            try { Debugger.Launch(); } catch { }
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation("Gold price feed service disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            _logger.LogWarning("Gold price feed URL is not configured. Service will not run.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(15, _options.IntervalSeconds));

        await RunOnceSafeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gold price feed loop failed");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunOnceSafeAsync(CancellationToken ct)
    {
        try
        {
            await RunOnceAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gold price feed iteration failed");
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var payload = await FetchPayloadAsync(ct);
        if (payload is null)
        {
            _logger.LogWarning("Gold price feed did not return a payload.");
            return;
        }

        using var scope = _sp.CreateScope();
        var market = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

        var entry = new GoldFeedNewVersion
        {
            Id = Guid.NewGuid(),
            RawResponse = payload,
            FetchTime = DateTime.UtcNow
        };

        GoldFeedParsedResult? parsed = null;
        if (GoldFeedNewVersionParser.TryParse(payload, out parsed, out var error))
        {
            entry.IsParsed = true;
            entry.ParseError = null;
            _logger.LogInformation("Gold price feed parsed successfully.");
        }
        else
        {
            entry.IsParsed = false;
            entry.ParseError = error;
            _logger.LogWarning("Gold price feed parse failed: {error}", error);
        }

        market.GoldFeedNewVersions.Add(entry);

        var wrotePrice = false;
        if (parsed is not null)
        {
            var has = parsed.Header.Has;
            if (has >= _options.MinimumPrice)
            {
                var writer = scope.ServiceProvider.GetRequiredService<IGoldPriceWriter>();
                await writer.UpsertAsync(has, null, _options.UserEmail, ct);
                wrotePrice = true;
                _logger.LogInformation("Has price updated from feed: {price}", has);
            }
            else
            {
                _logger.LogWarning("Has price from feed ignored (below minimum): {price}", has);
            }
        }

        if (!wrotePrice)
        {
            await market.SaveChangesAsync(ct);
        }
    }

    private async Task<string?> FetchPayloadAsync(CancellationToken ct)
    {
        try
        {
            var method = string.Equals(_options.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                ? HttpMethod.Post
                : HttpMethod.Get;

            using var req = new HttpRequestMessage(method, _options.Url);
            var client = _httpClientFactory.CreateClient("pricing-feed");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(3, _options.TimeoutSeconds));
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gold price feed returned status {status}", resp.StatusCode);
                return null;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var charset = resp.Content.Headers.ContentType?.CharSet;
            var encoding = TryGetEncoding(charset) ?? Encoding.UTF8;
            return encoding.GetString(bytes);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Gold price feed request timed out.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gold price feed fetch failed.");
            return null;
        }
    }

    private static Encoding? TryGetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return null;
        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return null;
        }
    }
}
