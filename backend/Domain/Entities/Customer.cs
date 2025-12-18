using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }

    [MaxLength(150)]
    public string AdSoyad { get; set; } = string.Empty;

    [MaxLength(160)]
    public string NormalizedAdSoyad { get; set; } = string.Empty;

    [MaxLength(11)]
    public string TCKN { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastTransactionAt { get; set; }
}
