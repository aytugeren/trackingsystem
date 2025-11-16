namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class GoldFeedEntry
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string? MetaTarih { get; set; }
    public string? Language { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SourceTime { get; set; }
}
