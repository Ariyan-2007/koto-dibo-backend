using FluentValidation;
using KotoDibo.Application.Common;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.Expenses.Interfaces;
using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Features.Expenses.Services;

public class ExpenseService : IExpenseService
{
    private readonly IRepository<Expense> _expenses;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateExpenseRequest> _createValidator;

    public ExpenseService(
        IRepository<Expense> expenses,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateExpenseRequest> createValidator)
    {
        _expenses = expenses;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
    }

    public async Task<ExpenseDto> CreateAsync(string userId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var today = LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        if (request.Date > today)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["Date cannot be in the future."],
            });
        }

        var expense = new Expense
        {
            UserId = userId,
            Amount = request.Amount,
            Category = request.Category.Trim(),
            Description = request.Description.Trim(),
            Date = request.Date,
        };

        await _expenses.AddAsync(expense, cancellationToken);
        return ToDto(expense);
    }

    public async Task<ExpenseDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var expense = await _expenses.GetByIdAsync(id, cancellationToken);
        if (expense is null || expense.UserId != userId)
        {
            throw new NotFoundException("Expense", id);
        }

        return ToDto(expense);
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(string userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;

        var expenses = await _expenses.FindAsync(
            e => e.UserId == userId && e.Date >= effectiveFrom && e.Date <= effectiveTo,
            cancellationToken);

        return expenses
            .OrderByDescending(e => e.Date)
            .Select(ToDto)
            .ToList();
    }

    private static ExpenseDto ToDto(Expense expense) => new()
    {
        Id = expense.Id,
        Amount = expense.Amount,
        Category = expense.Category,
        Description = expense.Description,
        Date = expense.Date,
    };
}
