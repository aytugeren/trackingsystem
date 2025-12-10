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
    public DbSet<GoldFeedAlert> GoldFeedAlerts => Set<GoldFeedAlert>();
    public DbSet<GoldFeedEntry> GoldFeedEntries => Set<GoldFeedEntry>();
    public DbSet<GlobalGoldPrice> GlobalGoldPrices => Set<GlobalGoldPrice>();

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

        modelBuilder.Entity<GoldFeedEntry>(e =>
        {
            e.ToTable("GoldFeedEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Payload).IsRequired().HasColumnType("text");
            e.Property(x => x.MetaTarih).HasMaxLength(100);
            e.Property(x => x.Language).HasMaxLength(16);
            e.Property(x => x.FetchedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.SourceTime).HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.FetchedAt);
        });

        modelBuilder.Entity<GoldFeedAlert>(e =>
        {
            e.ToTable("GoldFeedAlerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).IsRequired().HasColumnType("text");
            e.Property(x => x.Level).HasMaxLength(32);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.ResolvedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<InvoiceGoldSnapshot>(e =>
        {
            e.ToTable("InvoiceGoldSnapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.FinalSatis).HasPrecision(18, 3);
            e.HasIndex(x => x.InvoiceId).IsUnique();
        });

        modelBuilder.Entity<GlobalGoldPrice>(e =>
        {
            e.ToTable("GlobalGoldPrices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(18, 3);
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.UpdatedByEmail).HasMaxLength(200);
            e.HasIndex(x => x.UpdatedAt);
        });
    }
}
