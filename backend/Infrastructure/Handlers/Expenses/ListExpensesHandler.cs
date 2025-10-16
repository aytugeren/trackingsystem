using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Handlers.Expenses;

public class ListExpensesHandler : IListExpensesHandler
{
    private readonly KtpDbContext _db;

    public ListExpensesHandler(KtpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Expense>> HandleAsync(ListExpenses query, CancellationToken cancellationToken = default)
    {
        return await _db.Expenses
            .AsNoTracking()
            .OrderBy(x => x.Tarih)
            .ThenBy(x => x.SiraNo)
            .ToListAsync(cancellationToken);
    }
}

