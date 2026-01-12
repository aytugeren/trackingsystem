using KuyumculukTakipProgrami.Domain.Entities;

namespace KuyumculukTakipProgrami.Application.Gold.Formula;

public enum GoldFormulaMode
{
    Preview = 0,
    Finalize = 1
}

public enum GoldFormulaOperationType
{
    Invoice = 0,
    Expense = 1
}

public sealed record GoldFormulaContext(
    decimal Amount,
    decimal HasGoldPrice,
    decimal VatRate,
    ProductAccountingType AccountingType,
    decimal? ProductGram,
    GoldFormulaDirection Direction,
    GoldFormulaOperationType OperationType,
    decimal? AltinSatisFiyati);

public sealed class GoldCalculationResult
{
    public decimal Gram { get; init; }
    public decimal Amount { get; init; }
    public decimal GoldServiceAmount { get; init; }
    public decimal LaborGross { get; init; }
    public decimal LaborNet { get; init; }
    public decimal Vat { get; init; }
    public decimal UnitHasPriceUsed { get; init; }
}

public sealed class GoldFormulaEvaluationResult
{
    public GoldCalculationResult Result { get; init; } = new();
    public IReadOnlyDictionary<string, decimal> UsedVariables { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyList<string>? DebugSteps { get; init; }
}

public interface IGoldFormulaEngine
{
    GoldFormulaEvaluationResult Evaluate(string definitionJson, GoldFormulaContext context, GoldFormulaMode mode);
    void ValidateDefinition(string definitionJson);
}
