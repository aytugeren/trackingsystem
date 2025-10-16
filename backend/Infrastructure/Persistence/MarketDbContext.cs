using KuyumculukTakipProgrami.Domain.Entities.Market;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Persistence;

public class MarketDbContext : DbContext
{
    public MarketDbContext(DbContextOptions<MarketDbContext> options) : base(options)
    {
    }

    public DbSet<PriceSetting> PriceSettings => Set<PriceSetting>();
    public DbSet<PriceRecord> PriceRecords => Set<PriceRecord>();
    public DbSet<InvoiceGoldSnapshot> InvoiceGoldSnapshots => Set<InvoiceGoldSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("market");

        modelBuilder.Entity<PriceSetting>(e =>
        {
            e.ToTable("PriceSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.MarginBuy).HasPrecision(18, 2);
            e.Property(x => x.MarginSell).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PriceRecord>(e =>
        {
            e.ToTable("PriceRecords");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Alis).HasPrecision(18, 3);
            e.Property(x => x.Satis).HasPrecision(18, 3);
            e.Property(x => x.FinalAlis).HasPrecision(18, 3);
            e.Property(x => x.FinalSatis).HasPrecision(18, 3);
            e.HasIndex(x => new { x.Code, x.SourceTime }).IsUnique();
        });

        modelBuilder.Entity<InvoiceGoldSnapshot>(e =>
        {
            e.ToTable("InvoiceGoldSnapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.FinalSatis).HasPrecision(18, 3);
            e.HasIndex(x => x.InvoiceId).IsUnique();
        });
    }
}
