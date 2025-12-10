namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class GlobalGoldPrice
{
    public Guid Id { get; set; }
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
    public string? UpdatedByEmail { get; set; }
}
