using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.RecurringExpenses.DTOs;

namespace KotoDibo.Application.Features.RecurringExpenses.Interfaces;

public interface IRecurringExpenseService
{
    Task<RecurringExpenseDto> CreateAsync(string userId, CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default);

    Task<RecurringExpenseDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringExpenseDto>> GetAllAsync(string userId, bool includeInactive, CancellationToken cancellationToken = default);

    Task<RecurringExpenseDto> UpdateAsync(string userId, string id, UpdateRecurringExpenseRequest request, CancellationToken cancellationToken = default);

    Task<RecurringExpenseDto> DeactivateAsync(string userId, string id, CancellationToken cancellationToken = default);

    // Materializes every due occurrence (up to and including asOfDate) for the caller's active
    // recurring expenses into real Expense rows, advancing LastGeneratedDate/NextOccurrenceDate as
    // it goes. Safe to call as often as needed — see RecurringExpenseGenerator's idempotency note.
    Task<IReadOnlyList<ExpenseDto>> GenerateDueOccurrencesAsync(string userId, DateOnly asOfDate, CancellationToken cancellationToken = default);

    // Sweeps every active recurring expense across all users — used by the background generation
    // hosted service rather than per-request, since generation has no per-caller side effects that
    // need request context.
    Task GenerateDueOccurrencesForAllUsersAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);
}
