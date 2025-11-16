using System.Text.Json;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public interface IGoldPricingRefreshService
{
    Task<GoldPricingRefreshResult?> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed record GoldPricingRefreshResult(
    string Code,
    decimal Alis,
    decimal Satis,
    decimal FinalAlis,
    decimal FinalSatis,
    DateTime SourceTime,
    DateTime FetchedAt,
    string? MetaTarih);

public sealed class GoldPricingRefreshService : IGoldPricingRefreshService
{
    private const string DefaultFeedUrl = "https://canlipiyasalar.haremaltin.com/tmp/altin.json";
    private const int MaxFeedEntries = 100;
    private readonly IHttpClientFactory _httpFactory;
    private readonly MarketDbContext _market;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoldPricingRefreshService> _logger;

    public GoldPricingRefreshService(
        IHttpClientFactory httpFactory,
        MarketDbContext market,
        IConfiguration configuration,
        ILogger<GoldPricingRefreshService> logger)
    {
        _httpFactory = httpFactory;
        _market = market;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoldPricingRefreshResult?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var url = _configuration["Pricing:FeedUrl"] ?? DefaultFeedUrl;
        var lang = _configuration["Pricing:LanguageParam"] ?? "tr";
        try
        {
            var client = _httpFactory.CreateClient(nameof(GoldPricingRefreshService));
            using var response = await client.GetAsync($"{url}?dil_kodu={lang}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pricing feed responded with {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!PriceFeedParser.TryParseAltin(root, out var alis, out var satis, out var sourceTime))
            {
                _logger.LogWarning("ALTIN section was missing in the pricing feed");
                return null;
            }

            var metaTarih = PriceFeedParser.TryGetMetaTarih(root);
            var finalSourceTime = DateTime.SpecifyKind(sourceTime, DateTimeKind.Utc);
            var finalFetchedAt = DateTime.UtcNow;

            var setting = await _market.PriceSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "ALTIN", cancellationToken)
                ?? new PriceSetting { Code = "ALTIN" };

            var finalAlis = alis + setting.MarginBuy;
            var finalSatis = satis + setting.MarginSell;

            var exists = await _market.PriceRecords
                .AsNoTracking()
                .AnyAsync(x => x.Code == "ALTIN" && x.SourceTime == finalSourceTime, cancellationToken);

            if (!exists)
            {
                _market.PriceRecords.Add(new PriceRecord
                {
                    Id = Guid.NewGuid(),
                    Code = "ALTIN",
                    Alis = alis,
                    Satis = satis,
                    SourceTime = finalSourceTime,
                    FinalAlis = finalAlis,
                    FinalSatis = finalSatis,
                    CreatedAt = finalFetchedAt
                });
            }

            var feedEntry = new GoldFeedEntry
            {
                Id = Guid.NewGuid(),
                Payload = payload,
                MetaTarih = metaTarih,
                Language = lang,
                FetchedAt = finalFetchedAt,
                SourceTime = finalSourceTime
            };

            _market.GoldFeedEntries.Add(feedEntry);
            await _market.SaveChangesAsync(cancellationToken);
            await TrimFeedEntriesAsync(cancellationToken);
            await ClearActiveAlertsAsync(cancellationToken);

            return new GoldPricingRefreshResult(
                "ALTIN",
                alis,
                satis,
                finalAlis,
                finalSatis,
                finalSourceTime,
                finalFetchedAt,
                metaTarih);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RecordAlertAsync($"Su anda guncel fiyatlari cekilemiyor. {ex.Message}", cancellationToken);
            _logger.LogWarning(ex, "Failed to refresh gold pricing feed");
            return null;
        }
    }
    private async Task TrimFeedEntriesAsync(CancellationToken cancellationToken)
    {
        var total = await _market.GoldFeedEntries.CountAsync(cancellationToken);
        if (total <= MaxFeedEntries) return;
        var toRemove = await _market.GoldFeedEntries
            .OrderBy(x => x.FetchedAt)
            .Take(total - MaxFeedEntries)
            .ToListAsync(cancellationToken);
        _market.GoldFeedEntries.RemoveRange(toRemove);
        await _market.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearActiveAlertsAsync(CancellationToken cancellationToken)
    {
        var active = await _market.GoldFeedAlerts
            .Where(x => x.ResolvedAt == null)
            .ToListAsync(cancellationToken);
        if (active.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var alert in active) alert.ResolvedAt = now;
        await _market.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordAlertAsync(string message, CancellationToken cancellationToken)
    {
        var alert = new GoldFeedAlert
        {
            Id = Guid.NewGuid(),
            Message = message,
            Level = "warning",
            CreatedAt = DateTime.UtcNow
        };
        _market.GoldFeedAlerts.Add(alert);
        await _market.SaveChangesAsync(cancellationToken);
    }
}


