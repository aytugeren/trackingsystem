using KuyumculukTakipProgrami.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task EnsureSeededAsync(KtpDbContext db)
    {
        // Temiz veri: test seed ekleme (Ali Veli, Ahmet Demir vb.) kaldırıldı.
        // Bu metot ileride konfigürasyona bağlı idempotent seedler için boş bırakıldı.
        await Task.CompletedTask;
    }
}
