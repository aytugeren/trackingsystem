using KuyumculukTakipProgrami.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task EnsureSeededAsync(KtpDbContext db)
    {
        var hasInvoice = await db.Invoices.AnyAsync();
        var hasExpense = await db.Expenses.AnyAsync();

        if (!hasInvoice)
        {
            db.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                Tarih = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SiraNo = 1,
                MusteriAdSoyad = "Ali Veli",
                TCKN = "11111111111",
                Tutar = 12500.50m,
                OdemeSekli = OdemeSekli.KrediKarti
            });
        }

        if (!hasExpense)
        {
            db.Expenses.Add(new Expense
            {
                Id = Guid.NewGuid(),
                Tarih = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SiraNo = 1,
                MusteriAdSoyad = "Ahmet Demir",
                TCKN = "22222222222",
                Tutar = 3500m
            });
        }

        if (!hasInvoice || !hasExpense)
        {
            await db.SaveChangesAsync();
        }
    }
}

