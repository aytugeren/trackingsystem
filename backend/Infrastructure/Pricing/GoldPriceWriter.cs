using System;
using System.Threading;
using System.Threading.Tasks;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public interface IGoldPriceWriter
{
    Task<GlobalGoldPrice> UpsertAsync(decimal price, Guid? userId, string? email, CancellationToken ct);
}

public class GoldPriceWriter : IGoldPriceWriter
{
    private readonly MarketDbContext _market;

    public GoldPriceWriter(MarketDbContext market)
    {
        _market = market;
    }

    public async Task<GlobalGoldPrice> UpsertAsync(decimal price, Guid? userId, string? email, CancellationToken ct)
    {
        if (price <= 0) throw new ArgumentException("Price must be greater than zero.", nameof(price));

        var now = DateTime.UtcNow;
        var latest = await _market.GlobalGoldPrices
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
        {
            latest = new GlobalGoldPrice { Id = Guid.NewGuid() };
            _market.GlobalGoldPrices.Add(latest);
        }

        var rounded = Math.Round(price, 3);
        latest.Price = rounded;
        latest.UpdatedAt = now;
        latest.UpdatedById = userId;
        latest.UpdatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email;

        var exists = await _market.PriceRecords.AnyAsync(x => x.Code == "ALTIN" && x.SourceTime == now, ct);
        if (!exists)
        {
            _market.PriceRecords.Add(new PriceRecord
            {
                Id = Guid.NewGuid(),
                Code = "ALTIN",
                Alis = rounded,
                Satis = rounded,
                SourceTime = now,
                FinalAlis = rounded,
                FinalSatis = rounded,
                CreatedAt = now
            });
        }

        await _market.SaveChangesAsync(ct);
        return latest;
    }
}
