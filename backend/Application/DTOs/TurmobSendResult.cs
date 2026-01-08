namespace KuyumculukTakipProgrami.Application.DTOs;

public enum TurmobSendStatus
{
    Success,
    Failed,
    Skipped
}

public sealed class TurmobSendResult
{
    public TurmobSendStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public static TurmobSendResult Success()
    {
        return new TurmobSendResult { Status = TurmobSendStatus.Success };
    }

    public static TurmobSendResult Failed(string errorMessage)
    {
        return new TurmobSendResult { Status = TurmobSendStatus.Failed, ErrorMessage = errorMessage };
    }

    public static TurmobSendResult Skipped(string? reason = null)
    {
        return new TurmobSendResult { Status = TurmobSendStatus.Skipped, ErrorMessage = reason };
    }
}
