namespace KuyumculukTakipProgrami.Application.Invoices;

public record CreateInvoice(CreateInvoiceDto Dto);

public interface ICreateInvoiceHandler
{
    Task<Guid> HandleAsync(CreateInvoice command, CancellationToken cancellationToken = default);
}

