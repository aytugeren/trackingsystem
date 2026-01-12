using KuyumculukTakipProgrami.Application.Gold.Formula;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Services;
using Xunit;

public class GoldFormulaEngineTests
{
    private const string Default22Definition = "{\n  \"vars\": {\n    \"vatRate\": 0.20\n  },\n  \"steps\": [\n    { \"op\": \"set\", \"var\": \"safOran\", \"value\": 0.916 },\n    { \"op\": \"set\", \"var\": \"yeniOran\", \"value\": 0.99 },\n    { \"op\": \"calc\", \"var\": \"safAltin\", \"expr\": \"round(AltinSatisFiyati * safOran, 2)\" },\n    { \"op\": \"calc\", \"var\": \"tutar\", \"expr\": \"round(Amount, 2)\" },\n    { \"op\": \"calc\", \"var\": \"yeniUrun\", \"expr\": \"round(tutar * yeniOran)\" },\n    { \"op\": \"calc\", \"var\": \"gram\", \"expr\": \"safAltin==0 ? 0 : round(yeniUrun / safAltin)\" },\n    { \"op\": \"calc\", \"var\": \"altinHizmet\", \"expr\": \"round(gram * safAltin, 2)\" },\n    { \"op\": \"calc\", \"var\": \"iscilikKdvli\", \"expr\": \"round(tutar - altinHizmet, 2)\" },\n    { \"op\": \"calc\", \"var\": \"iscilikKdvHaric\", \"expr\": \"round(iscilikKdvli / (1 + vatRate))\" },\n    { \"op\": \"calc\", \"var\": \"kdv\", \"expr\": \"round(iscilikKdvli - iscilikKdvHaric, 2)\" }\n  ],\n  \"output\": {\n    \"gram\": \"gram\",\n    \"amount\": \"tutar\",\n    \"goldService\": \"altinHizmet\",\n    \"laborGross\": \"iscilikKdvli\",\n    \"laborNet\": \"iscilikKdvHaric\",\n    \"vat\": \"kdv\",\n    \"unitHasPriceUsed\": \"safAltin\"\n  }\n}";

    [Fact]
    public void Evaluate_DefaultFormula_Finalize_MatchesLegacy()
    {
        var engine = new GoldFormulaEngine();
        var amount = 1234.56m;
        var hasPrice = 1789.12m;
        var context = new GoldFormulaContext(
            amount,
            hasPrice,
            0.20m,
            ProductAccountingType.Gram,
            null,
            GoldFormulaDirection.Sale,
            GoldFormulaOperationType.Invoice,
            hasPrice);

        var result = engine.Evaluate(Default22Definition, context, GoldFormulaMode.Finalize).Result;

        var safAltin = R2(hasPrice * 0.916m);
        var tutar = R2(amount);
        var yeniUrun = R2(tutar * 0.99m);
        var gram = safAltin == 0 ? 0m : R2(yeniUrun / safAltin);
        var altinHizmet = R2(gram * safAltin);
        var iscilikKdvli = R2(R2(tutar) - altinHizmet);
        var iscilik = R2(iscilikKdvli / 1.20m);

        Assert.Equal(gram, result.Gram);
        Assert.Equal(tutar, result.Amount);
        Assert.Equal(altinHizmet, result.GoldServiceAmount);
        Assert.Equal(iscilikKdvli, result.LaborGross);
        Assert.Equal(iscilik, result.LaborNet);
        Assert.Equal(safAltin, result.UnitHasPriceUsed);
    }

    [Fact]
    public void Evaluate_DefaultFormula_Preview_MatchesLegacy()
    {
        var engine = new GoldFormulaEngine();
        var amount = 987.65m;
        var hasPrice = 2100.12m;
        var context = new GoldFormulaContext(
            amount,
            hasPrice,
            0.20m,
            ProductAccountingType.Gram,
            null,
            GoldFormulaDirection.Sale,
            GoldFormulaOperationType.Invoice,
            hasPrice);

        var result = engine.Evaluate(Default22Definition, context, GoldFormulaMode.Preview).Result;

        var safAltin = R2(hasPrice * 0.916m);
        var tutar = R2(amount);
        var yeniUrun = R3(tutar * 0.99m);
        var gram = safAltin == 0 ? 0m : R3(yeniUrun / safAltin);
        var altinHizmet = R2(gram * safAltin);
        var iscilikKdvli = R2(R2(tutar) - altinHizmet);
        var iscilik = R3(iscilikKdvli / 1.20m);

        Assert.Equal(gram, result.Gram);
        Assert.Equal(tutar, result.Amount);
        Assert.Equal(altinHizmet, result.GoldServiceAmount);
        Assert.Equal(iscilikKdvli, result.LaborGross);
        Assert.Equal(iscilik, result.LaborNet);
        Assert.Equal(safAltin, result.UnitHasPriceUsed);
    }

    [Fact]
    public void Evaluate_MissingOutput_Throws()
    {
        var engine = new GoldFormulaEngine();
        var definition = "{\"steps\":[{\"op\":\"set\",\"var\":\"x\",\"value\":1}],\"output\":{}}";
        var context = new GoldFormulaContext(
            10m,
            10m,
            0.20m,
            ProductAccountingType.Gram,
            null,
            GoldFormulaDirection.Sale,
            GoldFormulaOperationType.Invoice,
            10m);

        var ex = Assert.Throws<ArgumentException>(() => engine.Evaluate(definition, context, GoldFormulaMode.Finalize));
        Assert.Contains("gram", ex.Message);
    }

    private static decimal R2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal R3(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
