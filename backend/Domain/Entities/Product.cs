using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public enum ProductAccountingType
{
    Gram = 0,
    Adet = 1
}

public class Product
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool ShowInSales { get; set; } = true;

    // Gram veya adet bazli hesap tipi
    public ProductAccountingType AccountingType { get; set; } = ProductAccountingType.Gram;

    // Sabit gramli urunler icin (ornegin ceyrek altin)
    public decimal? Gram { get; set; }

    // Formul tanimli olmadan kullanilamaz
    public bool RequiresFormula { get; set; } = true;

    public Guid? DefaultFormulaId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedUserId { get; set; }
    public Guid? UpdatedUserId { get; set; }

    public ICollection<CategoryProduct> CategoryProducts { get; set; } = new List<CategoryProduct>();
}
