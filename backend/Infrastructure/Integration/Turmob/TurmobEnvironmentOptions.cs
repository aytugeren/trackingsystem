namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobEnvironmentOptions
{
    public string ServiceUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public string TemplateCode { get; set; } = string.Empty;
}
