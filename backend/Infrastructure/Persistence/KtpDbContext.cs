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
    public DbSet<User> Users => Set<User>();

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
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.OdemeSekli).HasConversion<int>();
            entity.Property(x => x.AltinSatisFiyati).HasPrecision(18, 3);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tarih).HasColumnType("date");
            entity.Property(x => x.SiraNo);
            entity.Property(x => x.MusteriAdSoyad).HasMaxLength(150);
            entity.Property(x => x.TCKN).HasMaxLength(11);
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.Role).HasConversion<int>().IsRequired();
        });
    }
}
