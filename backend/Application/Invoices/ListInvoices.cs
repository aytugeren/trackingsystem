using KuyumculukTakipProgrami.Domain.Entities;

namespace KuyumculukTakipProgrami.Application.Invoices;

public record ListInvoices();

public interface IListInvoicesHandler
{
    Task<IReadOnlyList<Invoice>> HandleAsync(ListInvoices query, CancellationToken cancellationToken = default);
}

