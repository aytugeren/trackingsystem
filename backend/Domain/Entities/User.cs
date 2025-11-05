using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public Role Role { get; set; }

    // Annual leave allowance (days). If null, defaults may apply on API side.
    public int? LeaveAllowanceDays { get; set; }

    // Fine-grained permissions
    public bool CanCancelInvoice { get; set; }
    public bool CanAccessLeavesAdmin { get; set; }

    // Working day hours for hour-based leave accounting (e.g., 8)
    public double? WorkingDayHours { get; set; }

    // Custom fun role assigned via Roles table (display only)
    public Guid? AssignedRoleId { get; set; }
    public string? CustomRoleName { get; set; }
}
