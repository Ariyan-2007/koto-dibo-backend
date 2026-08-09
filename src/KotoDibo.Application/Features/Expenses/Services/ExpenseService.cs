using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.Expenses.Interfaces;

namespace KotoDibo.Application.Features.Expenses.Services;

public class ExpenseService : IExpenseService
{
    public Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ExpenseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
