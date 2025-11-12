using Microsoft.Extensions.Configuration;
using System.Net.Http;
using KuyumculukTakipProgrami.Application.Common.Validation;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace KuyumculukTakipProgrami.Infrastructure.Handlers.Expenses;

public class CreateExpenseHandler : ICreateExpenseHandler
{
    private readonly KtpDbContext _db;
    private readonly MarketDbContext _market;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;

    public CreateExpenseHandler(KtpDbContext db, MarketDbContext market, IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _db = db;
        _market = market;
        _httpFactory = httpFactory;
        _cfg = cfg;
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

        // Fetch ALTIN price similar to invoice flow
        decimal? finalSatisFromLive = null;
        DateTime? sourceTimeFromLive = null;
        try
        {
            var url = _cfg["Pricing:FeedUrl"] ?? "https://canlipiyasalar.haremaltin.com/tmp/altin.json";
            var lang = _cfg["Pricing:LanguageParam"] ?? "tr";
            var client = _httpFactory.CreateClient();
            using var resp = await client.GetAsync($"{url}?dil_kodu={lang}", cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (TryParseAltin(json, out var alis, out var satis, out var srcTime))
                {
                    var codeByAyar = entity.AltinAyar == AltinAyar.Ayar22 ? "ALTIN_22" : "ALTIN_24";
                    var setting = await _market.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == codeByAyar, cancellationToken)
                                  ?? new PriceSetting { Code = codeByAyar, MarginBuy = 0, MarginSell = 0 };
                    var finalSatis = satis + setting.MarginSell;
                    finalSatisFromLive = finalSatis;
                    sourceTimeFromLive = srcTime;

                    var exists = await _market.PriceRecords.AnyAsync(x => x.Code == "ALTIN" && x.SourceTime == srcTime, cancellationToken);
                    if (!exists)
                    {
                        var rec = new PriceRecord
                        {
                            Id = Guid.NewGuid(),
                            Code = "ALTIN",
                            Alis = alis,
                            Satis = satis,
                            SourceTime = DateTime.SpecifyKind(srcTime, DateTimeKind.Utc),
                            FinalAlis = alis + setting.MarginBuy,
                            FinalSatis = finalSatis,
                            CreatedAt = DateTime.UtcNow
                        };
                        _market.PriceRecords.Add(rec);
                        await _market.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }
        catch { }

        if (finalSatisFromLive.HasValue)
        {
            entity.AltinSatisFiyati = finalSatisFromLive.Value;
        }
        else
        {
            var latestStored = await _market.PriceRecords
                .Where(x => x.Code == "ALTIN")
                .OrderByDescending(x => x.SourceTime)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestStored is not null)
            {
                var codeByAyar = entity.AltinAyar == AltinAyar.Ayar22 ? "ALTIN_22" : "ALTIN_24";
                var setting = await _market.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == codeByAyar, cancellationToken)
                              ?? new PriceSetting { Code = codeByAyar, MarginBuy = 0, MarginSell = 0 };
                entity.AltinSatisFiyati = latestStored.Satis + setting.MarginSell;
                sourceTimeFromLive = latestStored.SourceTime;
            }
        }

        _db.Expenses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static bool TryParseAltin(string json, out decimal alis, out decimal satis, out DateTime sourceTime)
    {
        alis = 0; satis = 0; sourceTime = DateTime.UtcNow;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            if (!data.TryGetProperty("ALTIN", out var altin)) return false;
            var alisStr = altin.GetProperty("alis").ToString();
            var satisStr = altin.GetProperty("satis").ToString();
            var tarihStr = altin.GetProperty("tarih").GetString();
            var ci = CultureInfo.InvariantCulture;
            alis = decimal.Parse(alisStr, ci);
            satis = decimal.Parse(satisStr, ci);
            if (!DateTime.TryParseExact(
                tarihStr,
                "dd-MM-yyyy HH:mm:ss",
                CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
                out sourceTime))
            {
                sourceTime = DateTime.UtcNow;
            }
            return true;
        }
        catch { return false; }
    }
}
