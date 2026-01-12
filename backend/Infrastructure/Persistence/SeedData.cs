using KuyumculukTakipProgrami.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KuyumculukTakipProgrami.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task EnsureSeededAsync(KtpDbContext db)
    {
        var now = DateTime.UtcNow;

        var template22SaleId = await EnsureTemplateAsync(
            db,
            "DEFAULT_22_SALE",
            "Default 22 Ayar Satış",
            GoldFormulaType.Sale,
            BuildDefaultFormulaDefinition(0.916m, 0.99m),
            now);

        var template22PurchaseId = await EnsureTemplateAsync(
            db,
            "DEFAULT_22_PURCHASE",
            "Default 22 Ayar Alış",
            GoldFormulaType.Purchase,
            BuildDefaultFormulaDefinition(0.916m, 0.99m),
            now);

        var template24SaleId = await EnsureTemplateAsync(
            db,
            "DEFAULT_24_SALE",
            "Default 24 Ayar Satış",
            GoldFormulaType.Sale,
            BuildDefaultFormulaDefinition(0.995m, 0.998m),
            now);

        var template24PurchaseId = await EnsureTemplateAsync(
            db,
            "DEFAULT_24_PURCHASE",
            "Default 24 Ayar Alış",
            GoldFormulaType.Purchase,
            BuildDefaultFormulaDefinition(0.995m, 0.998m),
            now);

        await db.SaveChangesAsync();

        await EnsureBindingAsync(db, "ALTIN_22", template22SaleId, template22PurchaseId, now);
        await EnsureBindingAsync(db, "ALTIN_24", template24SaleId, template24PurchaseId, now);
    }

    private static async Task<Guid> EnsureTemplateAsync(
        KtpDbContext db,
        string code,
        string name,
        GoldFormulaType formulaType,
        string definitionJson,
        DateTime createdAt)
    {
        var existing = await db.GoldFormulaTemplates.FirstOrDefaultAsync(x => x.Code == code);
        if (existing is not null) return existing.Id;

        var entity = new GoldFormulaTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Scope = GoldFormulaScope.DefaultSystem,
            FormulaType = formulaType,
            DslVersion = 1,
            DefinitionJson = definitionJson,
            IsActive = true,
            CreatedAt = createdAt
        };
        db.GoldFormulaTemplates.Add(entity);
        return entity.Id;
    }

    private static async Task EnsureBindingAsync(
        KtpDbContext db,
        string productCode,
        Guid saleTemplateId,
        Guid purchaseTemplateId,
        DateTime now)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Code.ToUpper() == productCode);
        if (product is null) return;

        product.RequiresFormula = true;
        if (!product.DefaultFormulaId.HasValue)
            product.DefaultFormulaId = saleTemplateId;

        await EnsureBindingRowAsync(db, product.Id, saleTemplateId, GoldFormulaDirection.Sale, now);
        await EnsureBindingRowAsync(db, product.Id, purchaseTemplateId, GoldFormulaDirection.Purchase, now);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureBindingRowAsync(
        KtpDbContext db,
        Guid productId,
        Guid templateId,
        GoldFormulaDirection direction,
        DateTime now)
    {
        var exists = await db.GoldProductFormulaBindings
            .AnyAsync(x => x.GoldProductId == productId && x.FormulaTemplateId == templateId && x.Direction == direction && x.IsActive);
        if (exists) return;

        db.GoldProductFormulaBindings.Add(new GoldProductFormulaBinding
        {
            Id = Guid.NewGuid(),
            GoldProductId = productId,
            FormulaTemplateId = templateId,
            Direction = direction,
            IsActive = true
        });
    }

    private static string BuildDefaultFormulaDefinition(decimal safOran, decimal yeniOran)
    {
        var safText = safOran.ToString(CultureInfo.InvariantCulture);
        var yeniText = yeniOran.ToString(CultureInfo.InvariantCulture);

        return $@"{{
  ""vars"": {{
    ""vatRate"": 0.20
  }},
  ""steps"": [
    {{ ""op"": ""set"", ""var"": ""safOran"", ""value"": {safText} }},
    {{ ""op"": ""set"", ""var"": ""yeniOran"", ""value"": {yeniText} }},
    {{ ""op"": ""calc"", ""var"": ""safAltin"", ""expr"": ""round(AltinSatisFiyati * safOran, 2)"" }},
    {{ ""op"": ""calc"", ""var"": ""tutar"", ""expr"": ""round(Amount, 2)"" }},
    {{ ""op"": ""calc"", ""var"": ""yeniUrun"", ""expr"": ""round(tutar * yeniOran)"" }},
    {{ ""op"": ""calc"", ""var"": ""gram"", ""expr"": ""safAltin==0 ? 0 : round(yeniUrun / safAltin)"" }},
    {{ ""op"": ""calc"", ""var"": ""altinHizmet"", ""expr"": ""round(gram * safAltin, 2)"" }},
    {{ ""op"": ""calc"", ""var"": ""iscilikKdvli"", ""expr"": ""round(tutar - altinHizmet, 2)"" }},
    {{ ""op"": ""calc"", ""var"": ""iscilikKdvHaric"", ""expr"": ""round(iscilikKdvli / (1 + vatRate))"" }},
    {{ ""op"": ""calc"", ""var"": ""kdv"", ""expr"": ""round(iscilikKdvli - iscilikKdvHaric, 2)"" }}
  ],
  ""output"": {{
    ""gram"": ""gram"",
    ""amount"": ""tutar"",
    ""goldService"": ""altinHizmet"",
    ""laborGross"": ""iscilikKdvli"",
    ""laborNet"": ""iscilikKdvHaric"",
    ""vat"": ""kdv"",
    ""unitHasPriceUsed"": ""safAltin""
  }}
}}";
    }
}
