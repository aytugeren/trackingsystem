using System.Globalization;
using System.Text.Json;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

internal static class PriceFeedParser
{
    public static bool TryParseAltin(JsonElement root, out decimal alis, out decimal satis, out DateTime sourceTime)
    {
        alis = 0;
        satis = 0;
        sourceTime = DateTime.UtcNow;
        try
        {
            if (!root.TryGetProperty("data", out var data)) return false;
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
        catch
        {
            return false;
        }
    }

    public static string? TryGetMetaTarih(JsonElement root)
    {
        if (root.TryGetProperty("meta", out var meta) && meta.TryGetProperty("tarih", out var tarih))
        {
            return tarih.GetString();
        }
        return null;
    }
}
