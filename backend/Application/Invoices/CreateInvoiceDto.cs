using KuyumculukTakipProgrami.Domain.Entities;

namespace KuyumculukTakipProgrami.Application.Invoices;

public class CreateInvoiceDto
{
    public DateOnly Tarih { get; set; }
    public int SiraNo { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? TCKN { get; set; }
    public decimal Tutar { get; set; }
    public OdemeSekli OdemeSekli { get; set; }
}

