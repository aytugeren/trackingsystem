using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Application.Invoices;

namespace KuyumculukTakipProgrami.Application.Common.Validation;

public static class DtoValidators
{
    public static IReadOnlyList<string> Validate(CreateInvoiceDto dto)
    {
        var errors = new List<string>();

        var name = dto.MusteriAdSoyad?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("MusteriAdSoyad boY olamaz.");

        var tckn = dto.TCKN?.Trim() ?? string.Empty;
        if (tckn.Length != 11 || !tckn.All(char.IsDigit))
            errors.Add("TCKN tam 11 haneli olmalŽñ ve sadece rakam iÇõermeli.");

        ValidateCompanyFields(dto.IsCompany, dto.VknNo, dto.CompanyName, errors);

        if (dto.Tutar <= 0)
            errors.Add("Tutar 0'dan bÇ¬yÇ¬k olmalŽñ.");

        if (dto.SiraNo < 1)
            errors.Add("SiraNo en az 1 olmalŽñ.");

        var ayarVal = (int)dto.AltinAyar;
        if (ayarVal != 22 && ayarVal != 24)
            errors.Add("AltinAyar yalnŽñzca 22 veya 24 olmalŽñdŽñr.");

        return errors;
    }

    public static IReadOnlyList<string> Validate(CreateExpenseDto dto)
    {
        var errors = new List<string>();

        var name = dto.MusteriAdSoyad?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("MusteriAdSoyad boY olamaz.");

        var tckn = dto.TCKN?.Trim() ?? string.Empty;
        if (tckn.Length != 11 || !tckn.All(char.IsDigit))
            errors.Add("TCKN tam 11 haneli olmalŽñ ve sadece rakam iÇõermeli.");

        ValidateCompanyFields(dto.IsCompany, dto.VknNo, dto.CompanyName, errors);

        if (dto.Tutar <= 0)
            errors.Add("Tutar 0'dan bÇ¬yÇ¬k olmalŽñ.");

        if (dto.SiraNo < 1)
            errors.Add("SiraNo en az 1 olmalŽñ.");

        var ayarVal = (int)dto.AltinAyar;
        if (ayarVal != 22 && ayarVal != 24)
            errors.Add("AltinAyar yalnŽñzca 22 veya 24 olmalŽñdŽñr.");

        return errors;
    }

    private static void ValidateCompanyFields(bool isCompany, string? vknNo, string? companyName, List<string> errors)
    {
        var trimmedVkn = vknNo?.Trim() ?? string.Empty;
        var trimmedCompany = companyName?.Trim() ?? string.Empty;

        if (isCompany)
        {
            if (string.IsNullOrWhiteSpace(trimmedCompany))
                errors.Add("CompanyName bos olamaz.");

            if (!IsValidVkn(trimmedVkn))
                errors.Add("VKN 10 haneli ve gecerli olmalidir.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(trimmedVkn) || !string.IsNullOrWhiteSpace(trimmedCompany))
                errors.Add("VKN/CompanyName icin IsCompany secilmelidir.");
        }
    }

    private static bool IsValidVkn(string vkn)
    {
        if (string.IsNullOrWhiteSpace(vkn)) return false;
        if (!vkn.All(char.IsDigit)) return false;
        if (vkn.Length != 10) return false;

        var digits = vkn.Select(c => c - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var digit = digits[i];
            var tmp = (digit + 10 - (i + 1)) % 10;
            var pow = (int)(Math.Pow(2, 9 - i) % 9);
            var res = (tmp * pow) % 9;
            if (tmp != 0 && res == 0) res = 9;
            sum += res;
        }
        var checkDigit = (10 - (sum % 10)) % 10;
        return digits[9] == checkDigit;
    }
}
