using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Application.Invoices;

namespace KuyumculukTakipProgrami.Application.Common.Validation;

public static class DtoValidators
{
    public static IReadOnlyList<string> Validate(CreateInvoiceDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.MusteriAdSoyad))
            errors.Add("MusteriAdSoyad boş olamaz.");

        if (string.IsNullOrWhiteSpace(dto.TCKN) || dto.TCKN.Length != 11 || !dto.TCKN.All(char.IsDigit))
            errors.Add("TCKN tam 11 haneli olmalı ve sadece rakam içermeli.");

        if (dto.Tutar <= 0)
            errors.Add("Tutar 0'dan büyük olmalı.");

        if (dto.SiraNo < 1)
            errors.Add("SiraNo en az 1 olmalı.");

        var ayarVal = (int)dto.AltinAyar;
        if (ayarVal != 22 && ayarVal != 24)
            errors.Add("AltinAyar yalnızca 22 veya 24 olmalıdır.");

        return errors;
    }

    public static IReadOnlyList<string> Validate(CreateExpenseDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.MusteriAdSoyad))
            errors.Add("MusteriAdSoyad boş olamaz.");

        if (string.IsNullOrWhiteSpace(dto.TCKN) || dto.TCKN.Length != 11 || !dto.TCKN.All(char.IsDigit))
            errors.Add("TCKN tam 11 haneli olmalı ve sadece rakam içermeli.");

        if (dto.Tutar <= 0)
            errors.Add("Tutar 0'dan büyük olmalı.");

        if (dto.SiraNo < 1)
            errors.Add("SiraNo en az 1 olmalı.");

        var ayarVal = (int)dto.AltinAyar;
        if (ayarVal != 22 && ayarVal != 24)
            errors.Add("AltinAyar yalnızca 22 veya 24 olmalıdır.");

        return errors;
    }
}

