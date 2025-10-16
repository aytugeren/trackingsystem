using KuyumculukTakipProgrami.Domain.Entities;

namespace KuyumculukTakipProgrami.Application.Expenses;

public record ListExpenses();

public interface IListExpensesHandler
{
    Task<IReadOnlyList<Expense>> HandleAsync(ListExpenses query, CancellationToken cancellationToken = default);
}

