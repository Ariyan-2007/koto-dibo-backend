using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Interfaces;

public interface IBudgetService
{
    Task<BudgetDto> CreateAsync(string userId, CreateBudgetRequest request, CancellationToken cancellationToken = default);

    Task<BudgetDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetSummaryDto>> GetAllAsync(string userId, string? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<BudgetDto> UpdateAsync(string userId, string id, UpdateBudgetRequest request, CancellationToken cancellationToken = default);

    Task<BudgetDto> AddCategoryAsync(string userId, string budgetId, AddBudgetCategoryRequest request, CancellationToken cancellationToken = default);

    Task<BudgetDto> AdjustCategoryAsync(string userId, string budgetId, string allocationId, AdjustBudgetCategoryRequest request, CancellationToken cancellationToken = default);

    Task<BudgetDto> TransferCategoryAsync(string userId, string budgetId, string fromAllocationId, TransferBudgetCategoryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetAdjustmentDto>> GetAdjustmentHistoryAsync(string userId, string budgetId, string allocationId, CancellationToken cancellationToken = default);

    // Creates the next period's Budget, carrying forward each rollover-enabled category's
    // remaining balance (which can be negative — an overspent envelope's deficit follows the user
    // into the next period rather than silently vanishing) as that category's new RolloverAmount.
    Task<BudgetDto> RolloverAsync(string userId, string budgetId, RolloverBudgetRequest request, CancellationToken cancellationToken = default);
}
