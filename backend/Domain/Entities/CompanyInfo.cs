using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class CompanyInfo
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(20)]
    public string? TaxNo { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? TradeRegistryNo { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    public DateTime UpdatedAt { get; set; }
}
