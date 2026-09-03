using KotoDibo.Application.Common;
using KotoDibo.Application.Features.Expenses.DTOs;

namespace KotoDibo.Application.Features.Expenses.Interfaces;

public interface IExpenseService
{
    Task<ExpenseDto> CreateAsync(string userId, CreateExpenseRequest request, CancellationToken cancellationToken = default);

    Task<ExpenseDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<PagedResult<ExpenseDto>> GetPagedAsync(string userId, ExpenseListQuery query, CancellationToken cancellationToken = default);

    Task<ExpenseDto> UpdateAsync(string userId, string id, UpdateExpenseRequest request, CancellationToken cancellationToken = default);

    // Soft-delete (FinancialEntryStatus.Cancelled) — financial records are never hard-deleted, same
    // posture as BazarPurchase/Contribution/BillSplit.
    Task<ExpenseDto> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);
}
