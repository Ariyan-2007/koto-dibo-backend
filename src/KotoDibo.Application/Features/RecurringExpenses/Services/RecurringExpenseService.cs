using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Application.Features.RecurringExpenses.DTOs;
using KotoDibo.Application.Features.RecurringExpenses.Interfaces;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.RecurringExpenses.Services;

public class RecurringExpenseService : IRecurringExpenseService
{
    private readonly IRepository<RecurringExpense> _recurringExpenses;
    private readonly IRepository<Expense> _expenses;
    private readonly IExpenseCategoryService _categories;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateRecurringExpenseRequest> _createValidator;
    private readonly IValidator<UpdateRecurringExpenseRequest> _updateValidator;

    public RecurringExpenseService(
        IRepository<RecurringExpense> recurringExpenses,
        IRepository<Expense> expenses,
        IExpenseCategoryService categories,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateRecurringExpenseRequest> createValidator,
        IValidator<UpdateRecurringExpenseRequest> updateValidator)
    {
        _recurringExpenses = recurringExpenses;
        _expenses = expenses;
        _categories = categories;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<RecurringExpenseDto> CreateAsync(string userId, CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var category = await _categories.RequireVisibleAsync(userId, request.CategoryId, cancellationToken);
        var frequency = Enum.Parse<RecurrenceFrequency>(request.Frequency, ignoreCase: true);

        var now = _dateTimeProvider.UtcNow;
        var recurring = new RecurringExpense
        {
            UserId = userId,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? Domain.Constants.CurrencyDefaults.DefaultCurrency : request.Currency.Trim().ToUpperInvariant(),
            CategoryId = category.Id,
            CategoryName = category.Name,
            Merchant = request.Merchant?.Trim(),
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim(),
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? ExpensePaymentMethod.Cash : Enum.Parse<ExpensePaymentMethod>(request.PaymentMethod, ignoreCase: true),
            Tags = NormalizeTags(request.Tags),
            Frequency = frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextOccurrenceDate = request.StartDate,
            LastGeneratedDate = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _recurringExpenses.AddAsync(recurring, cancellationToken);
        return ToDto(recurring);
    }

    public async Task<RecurringExpenseDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var recurring = await GetOwnedAsync(userId, id, cancellationToken);
        return ToDto(recurring);
    }

    public async Task<IReadOnlyList<RecurringExpenseDto>> GetAllAsync(string userId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringExpenses.FindAsync(r => r.UserId == userId, cancellationToken);
        return recurring
            .Where(r => includeInactive || r.IsActive)
            .OrderBy(r => r.NextOccurrenceDate)
            .Select(ToDto)
            .ToList();
    }

    public async Task<RecurringExpenseDto> UpdateAsync(string userId, string id, UpdateRecurringExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var recurring = await GetOwnedAsync(userId, id, cancellationToken);

        if (request.Amount is { } amount)
        {
            recurring.Amount = amount;
        }

        if (request.Currency is not null)
        {
            recurring.Currency = request.Currency.Trim().ToUpperInvariant();
        }

        if (request.CategoryId is not null)
        {
            var category = await _categories.RequireVisibleAsync(userId, request.CategoryId, cancellationToken);
            recurring.CategoryId = category.Id;
            recurring.CategoryName = category.Name;
        }

        if (request.Merchant is not null)
        {
            recurring.Merchant = request.Merchant.Trim();
        }

        if (request.Description is not null)
        {
            recurring.Description = request.Description.Trim();
        }

        if (request.Notes is not null)
        {
            recurring.Notes = request.Notes.Trim();
        }

        if (request.PaymentMethod is not null)
        {
            recurring.PaymentMethod = Enum.Parse<ExpensePaymentMethod>(request.PaymentMethod, ignoreCase: true);
        }

        if (request.Tags is not null)
        {
            recurring.Tags = NormalizeTags(request.Tags);
        }

        if (request.EndDate is not null)
        {
            if (request.EndDate < recurring.StartDate)
            {
                throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.EndDate)] = ["EndDate cannot be before StartDate."],
                });
            }

            recurring.EndDate = request.EndDate;
        }

        if (request.IsActive is not null)
        {
            recurring.IsActive = request.IsActive.Value;
        }

        recurring.UpdatedAt = _dateTimeProvider.UtcNow;
        await _recurringExpenses.UpdateAsync(recurring, cancellationToken);
        return ToDto(recurring);
    }

    public async Task<RecurringExpenseDto> DeactivateAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var recurring = await GetOwnedAsync(userId, id, cancellationToken);
        recurring.IsActive = false;
        recurring.UpdatedAt = _dateTimeProvider.UtcNow;
        await _recurringExpenses.UpdateAsync(recurring, cancellationToken);
        return ToDto(recurring);
    }

    public async Task<IReadOnlyList<ExpenseDto>> GenerateDueOccurrencesAsync(string userId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var due = await _recurringExpenses.FindAsync(
            r => r.UserId == userId && r.IsActive && r.NextOccurrenceDate <= asOfDate,
            cancellationToken);

        var generated = new List<ExpenseDto>();
        foreach (var recurring in due)
        {
            generated.AddRange(await GenerateForRecurringAsync(recurring, asOfDate, cancellationToken));
        }

        return generated;
    }

    public async Task GenerateDueOccurrencesForAllUsersAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var due = await _recurringExpenses.FindAsync(
            r => r.IsActive && r.NextOccurrenceDate <= asOfDate,
            cancellationToken);

        foreach (var recurring in due)
        {
            await GenerateForRecurringAsync(recurring, asOfDate, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<ExpenseDto>> GenerateForRecurringAsync(RecurringExpense recurring, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var occurrences = RecurringExpenseGenerator.GetDueOccurrences(recurring, asOfDate);
        if (occurrences.Count == 0)
        {
            return [];
        }

        var now = _dateTimeProvider.UtcNow;
        var created = new List<ExpenseDto>();
        var lastGenerated = recurring.LastGeneratedDate;

        foreach (var occurrenceDate in occurrences)
        {
            var expense = new Expense
            {
                UserId = recurring.UserId,
                Amount = recurring.Amount,
                Currency = recurring.Currency,
                CategoryId = recurring.CategoryId,
                CategoryName = recurring.CategoryName,
                Merchant = recurring.Merchant,
                Description = recurring.Description,
                Notes = recurring.Notes,
                Date = occurrenceDate,
                PaymentMethod = recurring.PaymentMethod,
                Tags = [.. recurring.Tags],
                RecurringExpenseId = recurring.Id,
                Status = FinancialEntryStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };

            try
            {
                await _expenses.AddAsync(expense, cancellationToken);
                created.Add(ToExpenseDto(expense));
                lastGenerated = occurrenceDate;
            }
            catch (DuplicateKeyException)
            {
                // Another generation run (a concurrent background sweep + manual trigger) already
                // materialized this exact occurrence — the unique (RecurringExpenseId, Date) index
                // caught it. Treat it as already-done and keep advancing.
                lastGenerated = occurrenceDate;
            }
        }

        if (lastGenerated != recurring.LastGeneratedDate)
        {
            recurring.LastGeneratedDate = lastGenerated;
            recurring.NextOccurrenceDate = RecurringExpenseGenerator.ComputeNextOccurrence(lastGenerated!.Value, recurring.Frequency);
            recurring.UpdatedAt = now;
            await _recurringExpenses.UpdateAsync(recurring, cancellationToken);
        }

        return created;
    }

    private async Task<RecurringExpense> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var recurring = await _recurringExpenses.GetByIdAsync(id, cancellationToken);
        if (recurring is null || recurring.UserId != userId)
        {
            throw new NotFoundException("RecurringExpense", id);
        }

        return recurring;
    }

    private static List<string> NormalizeTags(List<string>? tags) =>
        (tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static RecurringExpenseDto ToDto(RecurringExpense recurring) => new()
    {
        Id = recurring.Id,
        Amount = recurring.Amount,
        Currency = recurring.Currency,
        CategoryId = recurring.CategoryId,
        CategoryName = recurring.CategoryName,
        Merchant = recurring.Merchant,
        Description = recurring.Description,
        Notes = recurring.Notes,
        PaymentMethod = recurring.PaymentMethod.ToString(),
        Tags = recurring.Tags,
        Frequency = recurring.Frequency.ToString(),
        StartDate = recurring.StartDate,
        EndDate = recurring.EndDate,
        NextOccurrenceDate = recurring.NextOccurrenceDate,
        LastGeneratedDate = recurring.LastGeneratedDate,
        IsActive = recurring.IsActive,
        CreatedAt = recurring.CreatedAt,
        UpdatedAt = recurring.UpdatedAt,
    };

    private static ExpenseDto ToExpenseDto(Expense expense) => new()
    {
        Id = expense.Id,
        Amount = expense.Amount,
        Currency = expense.Currency,
        CategoryId = expense.CategoryId,
        CategoryName = expense.CategoryName,
        Merchant = expense.Merchant,
        Description = expense.Description,
        Notes = expense.Notes,
        Date = expense.Date,
        PaymentMethod = expense.PaymentMethod.ToString(),
        Tags = expense.Tags,
        ReceiptUrl = expense.ReceiptUrl,
        RecurringExpenseId = expense.RecurringExpenseId,
        IsRecurringGenerated = true,
        Status = expense.Status.ToString(),
        CreatedAt = expense.CreatedAt,
        UpdatedAt = expense.UpdatedAt,
    };
}
