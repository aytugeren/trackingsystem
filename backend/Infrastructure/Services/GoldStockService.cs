using KuyumculukTakipProgrami.Application.Gold;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Services;

public class GoldStockService : IGoldStockService
{
    private readonly KtpDbContext _db;

    public GoldStockService(KtpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GoldStockRow>> GetStockAsync(CancellationToken cancellationToken = default)
    {
        var openings = await _db.GoldOpeningInventories.AsNoTracking().ToListAsync(cancellationToken);
        var openingMap = openings.ToDictionary(x => x.Karat, x => x);

        var karatSet = new HashSet<int>(openingMap.Keys);
        var invKarats = await _db.Invoices.AsNoTracking()
            .Select(x => (int)x.AltinAyar)
            .Distinct()
            .ToListAsync(cancellationToken);
        var expKarats = await _db.Expenses.AsNoTracking()
            .Select(x => (int)x.AltinAyar)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var k in invKarats) karatSet.Add(k);
        foreach (var k in expKarats) karatSet.Add(k);

        var rows = new List<GoldStockRow>();
        foreach (var karat in karatSet.OrderByDescending(x => x))
        {
            if (!openingMap.TryGetValue(karat, out var opening))
            {
                rows.Add(new GoldStockRow(karat, 0m, 0m, 0m, 0m, null, null));
                continue;
            }

            var openingDate = DateOnly.FromDateTime(opening.Date);

            // Acilis tarihinden onceki hareketler hesaplamaya dahil edilmez.
            var expenseGram = await _db.Expenses.AsNoTracking()
                .Where(x => x.Kesildi && (int)x.AltinAyar == karat && x.Tarih >= openingDate)
                .Select(x => (decimal?)x.GramDegeri)
                .SumAsync(cancellationToken) ?? 0m;

            var invoiceGram = await _db.Invoices.AsNoTracking()
                .Where(x => x.Kesildi && (int)x.AltinAyar == karat && x.Tarih >= openingDate)
                .Select(x => (decimal?)x.GramDegeri)
                .SumAsync(cancellationToken) ?? 0m;

            var cashGram = opening.Gram + expenseGram - invoiceGram;
            rows.Add(new GoldStockRow(karat, opening.Gram, expenseGram, invoiceGram, cashGram, opening.Date, opening.Description));
        }

        return rows;
    }

    public async Task<GoldStockRow?> UpsertOpeningAsync(GoldOpeningInventoryInput input, CancellationToken cancellationToken = default)
    {
        var normalizedDate = NormalizeToUtc(input.Date);
        var opening = await _db.GoldOpeningInventories.FirstOrDefaultAsync(x => x.Karat == input.Karat, cancellationToken);
        if (opening is null)
        {
            opening = new GoldOpeningInventory
            {
                Id = Guid.NewGuid(),
                Karat = input.Karat,
                Date = normalizedDate,
                Gram = input.Gram,
                Description = input.Description,
                CreatedAt = DateTime.UtcNow,
            };
            _db.GoldOpeningInventories.Add(opening);
        }
        else
        {
            opening.Date = normalizedDate;
            opening.Gram = input.Gram;
            opening.Description = input.Description;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var rows = await GetStockAsync(cancellationToken);
        return rows.FirstOrDefault(x => x.Karat == input.Karat);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc) return value;
        if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
