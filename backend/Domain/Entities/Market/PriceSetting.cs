namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class PriceSetting
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "ALTIN"; // e.g., ALTIN
    public decimal MarginBuy { get; set; } // TL
    public decimal MarginSell { get; set; } // TL
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

