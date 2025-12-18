using System.Globalization;

namespace KuyumculukTakipProgrami.Infrastructure.Util;

public static class CustomerUtil
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return name.Trim().ToUpper(Tr);
    }

    public static string NormalizeTckn(string? tckn)
    {
        return string.IsNullOrWhiteSpace(tckn) ? string.Empty : tckn.Trim();
    }
}
