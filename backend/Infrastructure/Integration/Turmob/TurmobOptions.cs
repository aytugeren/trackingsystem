using System;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobOptions
{
    public const string SectionName = "Turmob";

    public bool Enabled { get; set; }
    public string Environment { get; set; } = "Test";
    public TurmobEnvironmentOptions Test { get; set; } = new();
    public TurmobEnvironmentOptions Prod { get; set; } = new();
    public TurmobRetryOptions Retry { get; set; } = new();
    public TurmobLoggingOptions Logging { get; set; } = new();

    public TurmobEnvironmentOptions? GetSelectedEnvironment()
    {
        if (string.Equals(Environment, "Prod", StringComparison.OrdinalIgnoreCase))
        {
            return Prod;
        }

        if (string.Equals(Environment, "Test", StringComparison.OrdinalIgnoreCase))
        {
            return Test;
        }

        return null;
    }
}

public sealed class TurmobRetryOptions
{
    public int Count { get; set; } = 3;
    public int DelaySeconds { get; set; } = 5;
}

public sealed class TurmobLoggingOptions
{
    public bool LogRequest { get; set; } = true;
    public bool LogResponse { get; set; } = true;
    public bool LogConnectionInfo { get; set; } = false;
}
