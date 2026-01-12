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

        var productOpenings = await (from p in _db.Products.AsNoTracking()
                                     join o in _db.ProductOpeningInventories.AsNoTracking() on p.Id equals o.ProductId
                                     where p.AccountingType == ProductAccountingType.Gram
                                     select new { p.Code, o.Date, o.Quantity }).ToListAsync(cancellationToken);
        var productOpeningMap = new Dictionary<int, (DateTime date, decimal gram)>();
        foreach (var row in productOpenings)
        {
            if (!int.TryParse(row.Code.Trim(), out var karat) || karat <= 0) continue;
            productOpeningMap[karat] = (row.Date, row.Quantity);
        }

        var karatSet = new HashSet<int>(openingMap.Keys);
        foreach (var k in productOpeningMap.Keys) karatSet.Add(k);
        var invKarats = await _db.Invoices.AsNoTracking()
            .Where(x => x.AltinAyar.HasValue)
            .Select(x => (int)x.AltinAyar!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var expKarats = await _db.Expenses.AsNoTracking()
            .Where(x => x.AltinAyar.HasValue)
            .Select(x => (int)x.AltinAyar!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var k in invKarats) karatSet.Add(k);
        foreach (var k in expKarats) karatSet.Add(k);

        var rows = new List<GoldStockRow>();
        foreach (var karat in karatSet.OrderByDescending(x => x))
        {
            var hasProductOpening = productOpeningMap.TryGetValue(karat, out var productOpening);
            var hasOpening = openingMap.TryGetValue(karat, out var opening);
            if (!hasProductOpening && !hasOpening)
            {
                rows.Add(new GoldStockRow(karat, 0m, 0m, 0m, 0m, null, null));
                continue;
            }

            var openingDateValue = hasProductOpening ? productOpening.date : opening!.Date;
            var openingGram = hasProductOpening ? productOpening.gram : opening!.Gram;
            var openingDate = DateOnly.FromDateTime(openingDateValue);
            var openingDescription = hasProductOpening ? "Ürün açılış envanteri" : opening?.Description;

            // Acilis tarihinden onceki hareketler hesaplamaya dahil edilmez.
            var expenseGram = await _db.Expenses.AsNoTracking()
                .Where(x => x.Kesildi && x.AltinAyar.HasValue && (int)x.AltinAyar.Value == karat && x.Tarih >= openingDate)
                .Select(x => (decimal?)x.GramDegeri)
                .SumAsync(cancellationToken) ?? 0m;

            var invoiceGram = await _db.Invoices.AsNoTracking()
                .Where(x => x.Kesildi && x.AltinAyar.HasValue && (int)x.AltinAyar.Value == karat && x.Tarih >= openingDate)
                .Select(x => (decimal?)x.GramDegeri)
                .SumAsync(cancellationToken) ?? 0m;

            var cashGram = openingGram + expenseGram - invoiceGram;
            rows.Add(new GoldStockRow(karat, openingGram, expenseGram, invoiceGram, cashGram, openingDateValue, openingDescription));
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
