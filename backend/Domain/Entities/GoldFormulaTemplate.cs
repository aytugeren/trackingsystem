using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public enum GoldFormulaScope
{
    DefaultSystem = 0,
    ProductSpecific = 1
}

public enum GoldFormulaType
{
    Purchase = 0,
    Sale = 1,
    Both = 2
}

public enum GoldFormulaDirection
{
    Purchase = 0,
    Sale = 1
}

public class GoldFormulaTemplate
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public GoldFormulaScope Scope { get; set; } = GoldFormulaScope.ProductSpecific;

    public GoldFormulaType FormulaType { get; set; } = GoldFormulaType.Both;

    public int DslVersion { get; set; } = 1;

    public string DefinitionJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
