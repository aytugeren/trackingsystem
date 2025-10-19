using KuyumculukTakipProgrami.Application.Common.Validation;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;

namespace KuyumculukTakipProgrami.Infrastructure.Handlers.Expenses;

public class CreateExpenseHandler : ICreateExpenseHandler
{
    private readonly KtpDbContext _db;

    public CreateExpenseHandler(KtpDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> HandleAsync(CreateExpense command, CancellationToken cancellationToken = default)
    {
        // Auto-assign SiraNo per date if not provided or invalid
        if (command.Dto.SiraNo < 1)
        {
            var max = _db.Expenses.Where(x => x.Tarih == command.Dto.Tarih).Select(x => (int?)x.SiraNo).Max();
            command.Dto.SiraNo = (max ?? 0) + 1;
        }

        var errors = DtoValidators.Validate(command.Dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" | ", errors));

        var entity = new Expense
        {
            Id = Guid.NewGuid(),
            Tarih = command.Dto.Tarih,
            SiraNo = command.Dto.SiraNo,
            MusteriAdSoyad = command.Dto.MusteriAdSoyad,
            TCKN = command.Dto.TCKN,
            Tutar = command.Dto.Tutar,
            KasiyerId = command.CurrentUserId
        };

        _db.Expenses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
