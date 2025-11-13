namespace PrintAgent;

public sealed class AgentSettings
{
    public int PollIntervalMs { get; init; } = 2000;
    public string? MachineName { get; init; }
}
