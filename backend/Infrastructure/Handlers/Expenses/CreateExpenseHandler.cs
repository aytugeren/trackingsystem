using KuyumculukTakipProgrami.Application.Common.Validation;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Infrastructure.Pricing;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using KuyumculukTakipProgrami.Infrastructure.Util;
using Microsoft.EntityFrameworkCore;

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

        var normalizedName = CustomerUtil.NormalizeName(command.Dto.MusteriAdSoyad);
        var normalizedTckn = CustomerUtil.NormalizeTckn(command.Dto.TCKN);
        var phone = command.Dto.Telefon?.Trim();
        var email = command.Dto.Email?.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.TCKN == normalizedTckn, cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                AdSoyad = normalizedName,
                NormalizedAdSoyad = normalizedName,
                TCKN = normalizedTckn,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                CreatedAt = DateTime.UtcNow,
                LastTransactionAt = DateTime.UtcNow
            };
            _db.Customers.Add(customer);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(normalizedName) && !string.Equals(customer.AdSoyad, normalizedName, StringComparison.Ordinal))
            {
                customer.AdSoyad = normalizedName;
                customer.NormalizedAdSoyad = normalizedName;
            }
            if (!string.IsNullOrWhiteSpace(phone))
                customer.Phone = phone;
            if (!string.IsNullOrWhiteSpace(email))
                customer.Email = email;
            customer.LastTransactionAt = DateTime.UtcNow;
        }

        var entity = new Expense
        {
            Id = Guid.NewGuid(),
            Tarih = command.Dto.Tarih,
            SiraNo = command.Dto.SiraNo,
            MusteriAdSoyad = customer.AdSoyad,
            TCKN = customer.TCKN,
            CustomerId = customer.Id,
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
