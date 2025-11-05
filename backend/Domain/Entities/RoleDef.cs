namespace KuyumculukTakipProgrami.Domain.Entities;

public class RoleDef
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Permission presets
    public bool CanCancelInvoice { get; set; }
    public bool CanAccessLeavesAdmin { get; set; }
    public int? LeaveAllowanceDays { get; set; }
    public double? WorkingDayHours { get; set; }
}

