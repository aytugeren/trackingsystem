namespace KuyumculukTakipProgrami.Domain.Entities;

public class RoleDef
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Permission presets
    public bool CanCancelInvoice { get; set; }
    public bool CanToggleKesildi { get; set; }
    public bool CanAccessLeavesAdmin { get; set; }
    public bool CanManageSettings { get; set; }
    public bool CanManageCashier { get; set; }
    public bool CanManageKarat { get; set; }
    public bool CanUseInvoices { get; set; }
    public bool CanUseExpenses { get; set; }
    public bool CanViewReports { get; set; }
    public int? LeaveAllowanceDays { get; set; }
    public double? WorkingDayHours { get; set; }
}
