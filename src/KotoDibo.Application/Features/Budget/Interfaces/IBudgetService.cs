using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Interfaces;

public interface IBudgetService
{
    Task<BudgetDto> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default);

    Task<BudgetDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
