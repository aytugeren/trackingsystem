namespace KuyumculukTakipProgrami.Application.Gold;

public record GoldStockRow(
    int Karat,
    decimal OpeningGram,
    decimal ExpenseGram,
    decimal InvoiceGram,
    decimal CashGram,
    DateTime? OpeningDate,
    string? Description
);

public record GoldOpeningInventoryInput(
    int Karat,
    DateTime Date,
    decimal Gram,
    string? Description
);
