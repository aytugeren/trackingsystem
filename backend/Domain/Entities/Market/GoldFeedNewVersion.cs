namespace KuyumculukTakipProgrami.Domain.Entities.Market;

public class GoldFeedNewVersion
{
    public Guid Id { get; set; }
    public string RawResponse { get; set; } = string.Empty;
    public DateTime FetchTime { get; set; } = DateTime.UtcNow;
    public bool IsParsed { get; set; }
    public string? ParseError { get; set; }
}
