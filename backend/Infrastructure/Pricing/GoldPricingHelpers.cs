using System;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public sealed record GoldPriceForAyar(decimal Price, DateTime SourceTime);

public static class GoldPricingHelpers
{
    public static async Task<GoldPriceForAyar?> GetLatestPriceForAyarAsync(this MarketDbContext market, AltinAyar ayar, bool useBuyMargin, CancellationToken ct)
    {
        var latest = await market.GlobalGoldPrices
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return null;

        var codeByAyar = ayar == AltinAyar.Ayar22 ? "ALTIN_22" : "ALTIN_24";
        var setting = await market.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == codeByAyar, ct)
                      ?? new PriceSetting { Code = codeByAyar };

        var price = useBuyMargin ? latest.Price - setting.MarginBuy : latest.Price + setting.MarginSell;
        if (useBuyMargin && price < 0) price = 0;

        return new GoldPriceForAyar(price, DateTime.SpecifyKind(latest.UpdatedAt, DateTimeKind.Utc));
    }

    public static async Task<PriceRecord?> GetLatestAltinRecordAsync(this MarketDbContext market, CancellationToken ct)
    {
        return await market.PriceRecords
            .Where(x => x.Code == "ALTIN")
            .OrderByDescending(x => x.SourceTime)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
