namespace KuyumculukTakipProgrami.Application.Gold;

public interface IGoldStockService
{
    Task<IReadOnlyList<GoldStockRow>> GetStockAsync(CancellationToken cancellationToken = default);
    Task<GoldStockRow?> UpsertOpeningAsync(GoldOpeningInventoryInput input, CancellationToken cancellationToken = default);
}
