namespace KuyumculukTakipProgrami.Application.Expenses;

public class CreateExpenseDto
{
    public DateOnly Tarih { get; set; }
    public int SiraNo { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? TCKN { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public decimal Tutar { get; set; }
    public KuyumculukTakipProgrami.Domain.Entities.AltinAyar AltinAyar { get; set; }
}
