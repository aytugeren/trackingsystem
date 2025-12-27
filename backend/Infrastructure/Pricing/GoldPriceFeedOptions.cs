using System;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public sealed class GoldPriceFeedOptions
{
    public bool Enabled { get; set; } = false;
    public string Url { get; set; } = string.Empty;
    public string PricePath { get; set; } = "price";
    public int IntervalSeconds { get; set; } = 240;
    public int TimeoutSeconds { get; set; } = 10;
    public string UserEmail { get; set; } = "dijikur@eren.com";
    public string HttpMethod { get; set; } = "GET";
    public decimal MinimumPrice { get; set; } = 0.01m;
    public bool BreakOnStart { get; set; } = false;
}
