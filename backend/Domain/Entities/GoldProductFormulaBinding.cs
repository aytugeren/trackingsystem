namespace KuyumculukTakipProgrami.Domain.Entities;

public class GoldProductFormulaBinding
{
    public Guid Id { get; set; }

    public Guid GoldProductId { get; set; }

    public Guid FormulaTemplateId { get; set; }

    public GoldFormulaDirection Direction { get; set; } = GoldFormulaDirection.Sale;

    public bool IsActive { get; set; } = true;
}
