namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class InvoiceGoldSnapshot
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string Code { get; set; } = "ALTIN";
    public decimal FinalSatis { get; set; }
    public DateTime SourceTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

