using KuyumculukTakipProgrami.Application.Common.Validation;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Infrastructure.Pricing;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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

        var nameUpper = command.Dto.MusteriAdSoyad;
        if (!string.IsNullOrWhiteSpace(nameUpper))
            nameUpper = nameUpper.Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

        var entity = new Invoice
        {
            Id = Guid.NewGuid(),
            Tarih = command.Dto.Tarih,
            SiraNo = command.Dto.SiraNo,
            MusteriAdSoyad = nameUpper,
            TCKN = command.Dto.TCKN,
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
