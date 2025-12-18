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

        if (dto.Tutar <= 0)
            errors.Add("Tutar 0'dan bÇ¬yÇ¬k olmalŽñ.");

        if (dto.SiraNo < 1)
            errors.Add("SiraNo en az 1 olmalŽñ.");

        var ayarVal = (int)dto.AltinAyar;
        if (ayarVal != 22 && ayarVal != 24)
            errors.Add("AltinAyar yalnŽñzca 22 veya 24 olmalŽñdŽñr.");

        return errors;
    }
}
