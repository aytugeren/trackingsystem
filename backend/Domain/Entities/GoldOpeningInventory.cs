using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class GoldOpeningInventory
{
    public Guid Id { get; set; }

    // Acilis tarihi (muhasebe bildirimi)
    public DateTime Date { get; set; }

    // Altin ayari (24, 22, 18 vb.)
    public int Karat { get; set; }

    public decimal Gram { get; set; }

    [MaxLength(250)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
