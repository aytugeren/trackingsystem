using KuyumculukTakipProgrami.Application.Common.Validation;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Infrastructure.Pricing;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KuyumculukTakipProgrami.Infrastructure.Handlers.Expenses;

public class CreateExpenseHandler : ICreateExpenseHandler
{
    private readonly KtpDbContext _db;
    private readonly MarketDbContext _market;

    public CreateExpenseHandler(KtpDbContext db, MarketDbContext market)
    {
        _db = db;
        _market = market;
    }

    public async Task<Guid> HandleAsync(CreateExpense command, CancellationToken cancellationToken = default)
    {
        // Always assign global, monotonically increasing SiraNo using DB sequence (never resets)
        command.Dto.SiraNo = await KuyumculukTakipProgrami.Infrastructure.Util.SequenceUtil
            .NextIntAsync(_db.Database, "Expenses_SiraNo_seq", initTable: "Expenses", initColumn: "SiraNo", ct: cancellationToken);

        var errors = DtoValidators.Validate(command.Dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" | ", errors));

        var nameUpper = command.Dto.MusteriAdSoyad;
        if (!string.IsNullOrWhiteSpace(nameUpper))
            nameUpper = nameUpper.Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

        var entity = new Expense
        {
            Id = Guid.NewGuid(),
            Tarih = command.Dto.Tarih,
            SiraNo = command.Dto.SiraNo,
            MusteriAdSoyad = nameUpper,
            TCKN = command.Dto.TCKN,
            Tutar = command.Dto.Tutar,
            AltinAyar = command.Dto.AltinAyar,
            KasiyerId = command.CurrentUserId
        };

        var priceData = await _market.GetLatestPriceForAyarAsync(entity.AltinAyar, useBuyMargin: true, cancellationToken);
        if (priceData is null)
            throw new ArgumentException("Has Altin fiyatı bulunamadı");
        entity.AltinSatisFiyati = priceData.Price;

        _db.Expenses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

}
