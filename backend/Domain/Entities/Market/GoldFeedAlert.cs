namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class GoldFeedAlert
{
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "warning";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
