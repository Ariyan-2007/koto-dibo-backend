using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Interfaces;

namespace KotoDibo.Application.Features.Budget.Services;

public class BudgetService : IBudgetService
{
    public Task<BudgetDto> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<BudgetDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<BudgetDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
