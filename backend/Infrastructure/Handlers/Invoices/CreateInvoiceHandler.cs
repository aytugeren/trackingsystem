using KuyumculukTakipProgrami.Application.Common.Validation;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Infrastructure.Pricing;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using KuyumculukTakipProgrami.Infrastructure.Util;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Handlers.Invoices;

public class CreateInvoiceHandler : ICreateInvoiceHandler
{
    private readonly KtpDbContext _db;
    private readonly MarketDbContext _market;
    public CreateInvoiceHandler(KtpDbContext db, MarketDbContext market)
    {
        _db = db;
        _market = market;
    }

    public async Task<Guid> HandleAsync(CreateInvoice command, CancellationToken cancellationToken = default)
    {
        // Always assign global, monotonically increasing SiraNo using DB sequence (never resets)
        command.Dto.SiraNo = await KuyumculukTakipProgrami.Infrastructure.Util.SequenceUtil
            .NextIntAsync(_db.Database, "Invoices_SiraNo_seq", initTable: "Invoices", initColumn: "SiraNo", ct: cancellationToken);

        var errors = DtoValidators.Validate(command.Dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" | ", errors));

        var normalizedName = CustomerUtil.NormalizeName(command.Dto.MusteriAdSoyad);
        var normalizedTckn = CustomerUtil.NormalizeTckn(command.Dto.TCKN);
        var normalizedCompanyName = CustomerUtil.NormalizeName(command.Dto.CompanyName);
        var normalizedVkn = CustomerUtil.NormalizeVkn(command.Dto.VknNo);
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
                IsCompany = command.Dto.IsCompany,
                VknNo = command.Dto.IsCompany && !string.IsNullOrWhiteSpace(normalizedVkn) ? normalizedVkn : null,
                CompanyName = command.Dto.IsCompany && !string.IsNullOrWhiteSpace(normalizedCompanyName) ? normalizedCompanyName : null,
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
            if (command.Dto.IsCompany)
            {
                customer.IsCompany = true;
                if (!string.IsNullOrWhiteSpace(normalizedVkn))
                    customer.VknNo = normalizedVkn;
                if (!string.IsNullOrWhiteSpace(normalizedCompanyName))
                    customer.CompanyName = normalizedCompanyName;
            }
            customer.LastTransactionAt = DateTime.UtcNow;
        }

        var entity = new Invoice
        {
            Id = Guid.NewGuid(),
            Tarih = command.Dto.Tarih,
            SiraNo = command.Dto.SiraNo,
            MusteriAdSoyad = customer.AdSoyad,
            TCKN = customer.TCKN,
            IsForCompany = command.Dto.IsForCompany,
            CustomerId = customer.Id,
            Tutar = command.Dto.Tutar,
            OdemeSekli = command.Dto.OdemeSekli,
            AltinAyar = command.Dto.AltinAyar,
            KasiyerId = command.CurrentUserId
        };
        
        DateTime? sourceTimeFromLive = null;
        var priceData = await _market.GetLatestPriceForAyarAsync(entity.AltinAyar, useBuyMargin: false, cancellationToken);
        if (priceData is null)
            throw new ArgumentException("Has Altin fiyatı bulunamadı");
        entity.AltinSatisFiyati = priceData.Price;
        sourceTimeFromLive = priceData.SourceTime;

        _db.Invoices.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // Snapshot current ALTIN final sell price if available
        // Create snapshot using live (preferred) or stored values
        if (entity.AltinSatisFiyati.HasValue && sourceTimeFromLive.HasValue)
        {
            var snap = new InvoiceGoldSnapshot
            {
                Id = Guid.NewGuid(),
                InvoiceId = entity.Id,
                Code = "ALTIN",
                FinalSatis = entity.AltinSatisFiyati.Value,
                SourceTime = DateTime.SpecifyKind(sourceTimeFromLive.Value, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow
            };
            _market.InvoiceGoldSnapshots.Add(snap);
            await _market.SaveChangesAsync(cancellationToken);
        }
        return entity.Id;
    }

}
