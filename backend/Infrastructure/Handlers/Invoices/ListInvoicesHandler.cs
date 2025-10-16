using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Infrastructure.Handlers.Invoices;

public class ListInvoicesHandler : IListInvoicesHandler
{
    private readonly KtpDbContext _db;

    public ListInvoicesHandler(KtpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Invoice>> HandleAsync(ListInvoices query, CancellationToken cancellationToken = default)
    {
        return await _db.Invoices
            .AsNoTracking()
            .OrderBy(x => x.Tarih)
            .ThenBy(x => x.SiraNo)
            .ToListAsync(cancellationToken);
    }
}

