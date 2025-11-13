using System;

namespace PrintAgent.Models;

public sealed class PrintJob
{
    public int Id { get; init; }
    public string? Zpl { get; init; }
    public bool IsPrinted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PrintedAt { get; init; }
    public string? MachineName { get; init; }
}
