using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Interfaces;

public interface IBudgetService
{
    Task<BudgetDto> CreateAsync(string userId, CreateBudgetRequest request, CancellationToken cancellationToken = default);

    Task<BudgetDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
}
