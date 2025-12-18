using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public DateOnly Tarih { get; set; }
    public int SiraNo { get; set; }

    [MaxLength(150)]
    public string? MusteriAdSoyad { get; set; }

    [MaxLength(11)]
    public string? TCKN { get; set; }

    public decimal Tutar { get; set; }

    public OdemeSekli OdemeSekli { get; set; }

    // Altın ayarı (22 / 24)
    public AltinAyar AltinAyar { get; set; }

    // O anki has altın satış fiyatı (marjlı)
    public decimal? AltinSatisFiyati { get; set; }

    // Finalization fields
    public decimal? SafAltinDegeri { get; set; }
    public decimal? UrunFiyati { get; set; }
    public decimal? YeniUrunFiyati { get; set; }
    public decimal? GramDegeri { get; set; }
    public decimal? Iscilik { get; set; }

    // Kasiyer onayı tamamlandığında işaretlemek için
    public DateTime? FinalizedAt { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Oluşturan kullanıcı bilgisi (JWT'den)
    public Guid? CreatedById { get; set; }
    public string? CreatedByEmail { get; set; }

    // Kasiyer (Users tablosuna FK)
    public Guid? KasiyerId { get; set; }
    public User? Kasiyer { get; set; }

    // Fatura durumu: kesildi/kesilmedi
    public bool Kesildi { get; set; }
}
