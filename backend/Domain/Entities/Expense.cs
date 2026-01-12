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

    public bool IsForCompany { get; set; }

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Tutar { get; set; }

    // O anki has altin satis fiyati (marjli)
    public decimal? AltinSatisFiyati { get; set; }

    // Altın ayarı (22 / 24) - ürün ayarsız olabilir
    public AltinAyar? AltinAyar { get; set; }

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

    // Olusturan kullanici bilgisi (JWT'den)
    public Guid? CreatedById { get; set; }
    public string? CreatedByEmail { get; set; }

    // Kasiyer (Users tablosuna FK)
    public Guid? KasiyerId { get; set; }
    public User? Kasiyer { get; set; }

    // Gider durumu: kesildi/kesilmedi
    public bool Kesildi { get; set; }
}
