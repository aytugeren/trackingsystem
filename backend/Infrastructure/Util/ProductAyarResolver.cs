using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace KuyumculukTakipProgrami.Infrastructure.Util;

public static class ProductAyarResolver
{
    private static readonly Regex Ayar24Regex = new Regex(@"(^|[^0-9])24([^0-9]|$)|24\s*ayar|24k", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Ayar22Regex = new Regex(@"(^|[^0-9])22([^0-9]|$)|22\s*ayar|22k", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<AltinAyar?> TryResolveAsync(KtpDbContext db, Guid productId, CancellationToken ct)
    {
        if (productId == Guid.Empty) return null;

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, ct);
        if (product is null) return null;

        var fromProduct = TryInferFromText($"{product.Name} {product.Code}");
        if (fromProduct.HasValue) return fromProduct;

        var categoryNames = await (from cp in db.CategoryProducts.AsNoTracking()
                                   join c in db.Categories.AsNoTracking() on cp.CategoryId equals c.Id
                                   where cp.ProductId == productId
                                   select c.Name).ToListAsync(ct);

        foreach (var name in categoryNames)
        {
            var inferred = TryInferFromText(name);
            if (inferred.HasValue) return inferred;
        }

        return null;
    }

    private static AltinAyar? TryInferFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (Ayar24Regex.IsMatch(text)) return AltinAyar.Ayar24;
        if (Ayar22Regex.IsMatch(text)) return AltinAyar.Ayar22;
        return null;
    }
}
