using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        var price = await FetchPriceAsync(ct);
        if (price.HasValue && price.Value >= _options.MinimumPrice)
        {
            using var scope = _sp.CreateScope();
            var writer = scope.ServiceProvider.GetRequiredService<IGoldPriceWriter>();
            var userId = await ResolveUserIdAsync(scope.ServiceProvider, ct);
            await writer.UpsertAsync(price.Value, userId, _options.UserEmail, ct);
            _logger.LogInformation("Gold price feed updated has altin to {price}", price.Value);
        }
        else
        {
            _logger.LogWarning("Gold price feed did not produce a valid price (value={price})", price);
        }
    }

    private async Task<Guid?> ResolveUserIdAsync(IServiceProvider provider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.UserEmail)) return null;

        try
        {
            var db = provider.GetRequiredService<KtpDbContext>();
            var email = _options.UserEmail.Trim().ToLowerInvariant();
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email, ct);
            return user?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve user id for {email}", _options.UserEmail);
            return null;
        }
    }

    private async Task<decimal?> FetchPriceAsync(CancellationToken ct)
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
            var payload = encoding.GetString(bytes);
            var price = ParsePrice(payload);
            if (!price.HasValue)
            {
                _logger.LogWarning("Gold price feed payload could not be parsed.");
            }
            return price;
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

    private decimal? ParsePrice(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;

        if (TryParseDecimal(payload, out var plain)) return plain;

        var hasMatch = Regex.Match(payload, @"\bHAS\s+([0-9]+(?:[.,][0-9]+)?)", RegexOptions.IgnoreCase);
        if (hasMatch.Success && TryParseDecimal(hasMatch.Groups[1].Value, out var hasVal)) return hasVal;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var paths = new[] { _options.PricePath, "price", "satis", "sell", "data.price" }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                if (TryReadPath(root, path, out var val)) return val;
            }
        }
        catch (JsonException)
        {
            // ignore, fallback returns null
        }

        return null;
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

    private static bool TryReadPath(JsonElement root, string path, out decimal value)
    {
        value = 0;
        var current = root;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return false;
        }

        return TryConvert(current, out value);
    }

    private static bool TryConvert(JsonElement element, out decimal value)
    {
        value = 0;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);
            case JsonValueKind.String:
                return TryParseDecimal(element.GetString() ?? string.Empty, out value);
            default:
                return false;
        }
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        value = 0;
        var normalized = text.Trim();
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return true;
        if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("tr-TR"), out value)) return true;
        return false;
    }
}
