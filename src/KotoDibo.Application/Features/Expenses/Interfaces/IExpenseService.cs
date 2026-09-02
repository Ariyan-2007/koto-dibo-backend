using KotoDibo.Application.Features.Expenses.DTOs;

namespace KotoDibo.Application.Features.Expenses.Interfaces;

public interface IExpenseService
{
    Task<ExpenseDto> CreateAsync(string userId, CreateExpenseRequest request, CancellationToken cancellationToken = default);

    Task<ExpenseDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseDto>> GetAllAsync(string userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
