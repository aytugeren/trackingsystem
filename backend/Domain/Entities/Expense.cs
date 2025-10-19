using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }
    public DateOnly Tarih { get; set; }
    public int SiraNo { get; set; }

    [MaxLength(150)]
    public string? MusteriAdSoyad { get; set; }

    [MaxLength(11)]
    public string? TCKN { get; set; }

    public decimal Tutar { get; set; }

    // Oluşturan kullanıcı bilgisi (JWT'den)
    public Guid? CreatedById { get; set; }
    public string? CreatedByEmail { get; set; }

    // Kasiyer (Users tablosuna FK)
    public Guid? KasiyerId { get; set; }
    public User? Kasiyer { get; set; }
}
