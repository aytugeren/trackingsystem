using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class Leave
{
    public Guid Id { get; set; }

    // Date-only range
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    // Optional time range for partial-day leaves (same-day)
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    // Creator info
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    public DateTime CreatedAt { get; set; }

    public LeaveStatus Status { get; set; }
}
