namespace KuyumculukTakipProgrami.Application.Expenses;

public record CreateExpense(CreateExpenseDto Dto, Guid? CurrentUserId);

public interface ICreateExpenseHandler
{
    Task<Guid> HandleAsync(CreateExpense command, CancellationToken cancellationToken = default);
}
