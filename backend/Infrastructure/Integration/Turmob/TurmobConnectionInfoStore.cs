using System;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobConnectionInfoStore
{
    private readonly object _lock = new();
    private TurmobConnectionInfo? _last;

    public TurmobConnectionInfo? GetLast()
    {
        lock (_lock)
        {
            return _last;
        }
    }

    public void Update(string? host, int? port, string? localEndpoint, string? remoteEndpoint)
    {
        var info = new TurmobConnectionInfo
        {
            Host = host,
            Port = port,
            LocalEndpoint = localEndpoint,
            RemoteEndpoint = remoteEndpoint,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        lock (_lock)
        {
            _last = info;
        }
    }
}

public sealed class TurmobConnectionInfo
{
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? LocalEndpoint { get; init; }
    public string? RemoteEndpoint { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
