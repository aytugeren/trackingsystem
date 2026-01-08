using KuyumculukTakipProgrami.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Persistence;

public class KtpDbContext : DbContext
{
    public KtpDbContext(DbContextOptions<KtpDbContext> options) : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<RoleDef> Roles => Set<RoleDef>();
    public DbSet<CompanyInfo> CompanyInfos => Set<CompanyInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tarih).HasColumnType("date");
            entity.Property(x => x.SiraNo);
            entity.Property(x => x.MusteriAdSoyad).HasMaxLength(150);
            entity.Property(x => x.TCKN).HasMaxLength(11);
            entity.Property(x => x.IsForCompany).IsRequired();
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.OdemeSekli).HasConversion<int>();
            entity.Property(x => x.AltinAyar).HasConversion<int>();
            entity.Property(x => x.AltinSatisFiyati).HasPrecision(18, 3);
            entity.Property(x => x.SafAltinDegeri).HasPrecision(18, 3);
            entity.Property(x => x.UrunFiyati).HasPrecision(18, 2);
            entity.Property(x => x.YeniUrunFiyati).HasPrecision(18, 3);
            entity.Property(x => x.GramDegeri).HasPrecision(18, 3);
            entity.Property(x => x.Iscilik).HasPrecision(18, 3);
            entity.HasOne(x => x.Customer)
                  .WithMany()
                  .HasForeignKey(x => x.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
            // Kasiyer iliskisi (nullable, kullanici silinirse null kalir)
            entity.HasOne(x => x.Kasiyer)
                  .WithMany()
                  .HasForeignKey(x => x.KasiyerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tarih).HasColumnType("date");
            entity.Property(x => x.SiraNo);
            entity.Property(x => x.MusteriAdSoyad).HasMaxLength(150);
            entity.Property(x => x.TCKN).HasMaxLength(11);
            entity.Property(x => x.IsForCompany).IsRequired();
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.AltinAyar).HasConversion<int>();
            entity.Property(x => x.SafAltinDegeri).HasPrecision(18, 3);
            entity.Property(x => x.UrunFiyati).HasPrecision(18, 2);
            entity.Property(x => x.YeniUrunFiyati).HasPrecision(18, 3);
            entity.Property(x => x.GramDegeri).HasPrecision(18, 3);
            entity.Property(x => x.Iscilik).HasPrecision(18, 3);
            entity.HasOne(x => x.Customer)
                  .WithMany()
                  .HasForeignKey(x => x.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
            // Kasiyer iliskisi (nullable)
            entity.HasOne(x => x.Kasiyer)
                  .WithMany()
                  .HasForeignKey(x => x.KasiyerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.Role).HasConversion<int>().IsRequired();
            entity.Property(x => x.LeaveAllowanceDays);
            entity.Property(x => x.CanCancelInvoice);
            entity.Property(x => x.CanAccessLeavesAdmin);
            entity.Property(x => x.WorkingDayHours);
        });

        modelBuilder.Entity<Leave>(entity =>
        {
            entity.ToTable("Leaves");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.From).HasColumnType("date");
            entity.Property(x => x.To).HasColumnType("date");
            entity.Property(x => x.FromTime).HasColumnType("time").IsRequired(false);
            entity.Property(x => x.ToTime).HasColumnType("time").IsRequired(false);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.UserEmail).HasMaxLength(200);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.Status).HasConversion<int>();
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KeyName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.KeyName).IsUnique();
            entity.Property(x => x.Value).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<RoleDef>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<CompanyInfo>(entity =>
        {
            entity.ToTable("CompanyInfos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.TaxNo).HasMaxLength(20);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.TradeRegistryNo).HasMaxLength(100);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.CityName).HasMaxLength(100);
            entity.Property(x => x.TownName).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.TaxOfficeName).HasMaxLength(100);
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AdSoyad).HasMaxLength(150).IsRequired();
            entity.Property(x => x.NormalizedAdSoyad).HasMaxLength(160).IsRequired();
            entity.Property(x => x.TCKN).HasMaxLength(11).IsRequired();
            entity.Property(x => x.IsCompany).IsRequired();
            entity.Property(x => x.VknNo).HasMaxLength(10).IsRequired(false);
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired(false);
            entity.Property(x => x.Phone).HasMaxLength(40).IsRequired(false);
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.LastTransactionAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.TCKN).IsUnique();
            entity.HasIndex(x => x.NormalizedAdSoyad);
        });
    }
}
