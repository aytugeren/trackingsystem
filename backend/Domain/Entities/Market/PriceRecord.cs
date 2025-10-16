namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class PriceRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "ALTIN";
    public decimal Alis { get; set; }
    public decimal Satis { get; set; }
    public DateTime SourceTime { get; set; }
    public decimal FinalAlis { get; set; }
    public decimal FinalSatis { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

