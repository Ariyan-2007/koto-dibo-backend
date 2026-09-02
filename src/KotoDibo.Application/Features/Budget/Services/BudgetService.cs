using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Interfaces;
using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Features.Budget.Services;

public class BudgetService : IBudgetService
{
    private readonly IRepository<Domain.Entities.Budget> _budgets;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateBudgetRequest> _createValidator;

    public BudgetService(
        IRepository<Domain.Entities.Budget> budgets,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateBudgetRequest> createValidator)
    {
        _budgets = budgets;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
    }

    public async Task<BudgetDto> CreateAsync(string userId, CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existing = await _budgets.FindOneAsync(b => b.UserId == userId && b.Period == request.Period, cancellationToken);
        if (existing is not null)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Period)] = [$"A budget for period '{request.Period}' already exists."],
            });
        }

        var budget = new Domain.Entities.Budget
        {
            UserId = userId,
            Period = request.Period,
            Amount = request.Amount,
            CreatedAt = _dateTimeProvider.UtcNow,
        };

        await _budgets.AddAsync(budget, cancellationToken);
        return ToDto(budget);
    }

    public async Task<BudgetDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var budget = await _budgets.GetByIdAsync(id, cancellationToken);
        if (budget is null || budget.UserId != userId)
        {
            throw new NotFoundException("Budget", id);
        }

        return ToDto(budget);
    }

    public async Task<IReadOnlyList<BudgetDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var budgets = await _budgets.FindAsync(b => b.UserId == userId, cancellationToken);
        return budgets
            .OrderByDescending(b => b.Period)
            .Select(ToDto)
            .ToList();
    }

    private static BudgetDto ToDto(Domain.Entities.Budget budget) => new()
    {
        Id = budget.Id,
        Period = budget.Period,
        Amount = budget.Amount,
    };
}
