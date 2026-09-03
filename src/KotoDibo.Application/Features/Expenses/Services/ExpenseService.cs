using FluentValidation;
using KotoDibo.Application.Common;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.Expenses.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;

namespace KotoDibo.Application.Features.Expenses.Services;

public class ExpenseService : IExpenseService
{
    private readonly IRepository<Expense> _expenses;
    private readonly IExpenseCategoryService _categories;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateExpenseRequest> _createValidator;
    private readonly IValidator<UpdateExpenseRequest> _updateValidator;

    public ExpenseService(
        IRepository<Expense> expenses,
        IExpenseCategoryService categories,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateExpenseRequest> createValidator,
        IValidator<UpdateExpenseRequest> updateValidator)
    {
        _expenses = expenses;
        _categories = categories;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ExpenseDto> CreateAsync(string userId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        RequireNotFuture(request.Date);

        var category = await _categories.RequireVisibleAsync(userId, request.CategoryId, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var expense = new Expense
        {
            UserId = userId,
            Amount = request.Amount,
            Currency = NormalizeCurrency(request.Currency),
            CategoryId = category.Id,
            CategoryName = category.Name,
            Merchant = request.Merchant?.Trim(),
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim(),
            Date = request.Date,
            PaymentMethod = ParsePaymentMethod(request.PaymentMethod),
            Tags = NormalizeTags(request.Tags),
            ReceiptUrl = request.ReceiptUrl?.Trim(),
            Status = FinancialEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _expenses.AddAsync(expense, cancellationToken);
        return ToDto(expense);
    }

    public async Task<ExpenseDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var expense = await GetOwnedAsync(userId, id, cancellationToken);
        return ToDto(expense);
    }

    public async Task<PagedResult<ExpenseDto>> GetPagedAsync(string userId, ExpenseListQuery query, CancellationToken cancellationToken = default)
    {
        var effectiveFrom = query.From ?? DateOnly.MinValue;
        var effectiveTo = query.To ?? DateOnly.MaxValue;
        var minAmount = query.MinAmount ?? decimal.MinValue;
        var maxAmount = query.MaxAmount ?? decimal.MaxValue;
        var categoryId = query.CategoryId;

        // UserId/Status/Date/Amount are unconditional comparisons (MinValue/MaxValue sentinels
        // stand in for "no filter"), so the Mongo LINQ provider pushes them straight down to the
        // server as a filter — that's what keeps a bounded window, not the whole collection,
        // coming back over the wire. CategoryId and the remaining filters (free text, tag
        // membership, merchant search) are optional-closure comparisons that are safer evaluated
        // in-memory below than trusted to translate through the query provider.
        var candidates = await _expenses.FindAsync(
            e => e.UserId == userId
                && e.Status == FinancialEntryStatus.Active
                && e.Date >= effectiveFrom && e.Date <= effectiveTo
                && e.Amount >= minAmount && e.Amount <= maxAmount,
            cancellationToken);

        IEnumerable<Expense> filtered = candidates;

        if (categoryId is not null)
        {
            filtered = filtered.Where(e => e.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.Merchant))
        {
            filtered = filtered.Where(e => e.Merchant is not null && e.Merchant.Contains(query.Merchant, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentMethod) && Enum.TryParse<ExpensePaymentMethod>(query.PaymentMethod, ignoreCase: true, out var paymentMethod))
        {
            filtered = filtered.Where(e => e.PaymentMethod == paymentMethod);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filtered = filtered.Where(e => e.Tags.Any(t => string.Equals(t, query.Tag, StringComparison.OrdinalIgnoreCase)));
        }

        if (query.IsRecurring is not null)
        {
            filtered = filtered.Where(e => query.IsRecurring.Value ? e.RecurringExpenseId != null : e.RecurringExpenseId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search;
            filtered = filtered.Where(e =>
                (e.Description is not null && e.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                || (e.Merchant is not null && e.Merchant.Contains(search, StringComparison.OrdinalIgnoreCase))
                || e.CategoryName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        filtered = ApplySort(filtered, query.SortBy, query.SortDescending);

        var materialized = filtered.ToList();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pageItems = materialized.Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToList();

        return PagedResult<ExpenseDto>.Create(pageItems, page, pageSize, materialized.Count);
    }

    public async Task<ExpenseDto> UpdateAsync(string userId, string id, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var expense = await GetOwnedAsync(userId, id, cancellationToken);
        RequireActive(expense);

        if (request.Date is { } date)
        {
            RequireNotFuture(date);
            expense.Date = date;
        }

        if (request.Amount is { } amount)
        {
            expense.Amount = amount;
        }

        if (request.Currency is not null)
        {
            expense.Currency = NormalizeCurrency(request.Currency);
        }

        if (request.CategoryId is not null)
        {
            var category = await _categories.RequireVisibleAsync(userId, request.CategoryId, cancellationToken);
            expense.CategoryId = category.Id;
            expense.CategoryName = category.Name;
        }

        if (request.Merchant is not null)
        {
            expense.Merchant = request.Merchant.Trim();
        }

        if (request.Description is not null)
        {
            expense.Description = request.Description.Trim();
        }

        if (request.Notes is not null)
        {
            expense.Notes = request.Notes.Trim();
        }

        if (request.PaymentMethod is not null)
        {
            expense.PaymentMethod = ParsePaymentMethod(request.PaymentMethod);
        }

        if (request.Tags is not null)
        {
            expense.Tags = NormalizeTags(request.Tags);
        }

        if (request.ReceiptUrl is not null)
        {
            expense.ReceiptUrl = request.ReceiptUrl.Trim();
        }

        expense.UpdatedAt = _dateTimeProvider.UtcNow;
        await _expenses.UpdateAsync(expense, cancellationToken);
        return ToDto(expense);
    }

    public async Task<ExpenseDto> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var expense = await GetOwnedAsync(userId, id, cancellationToken);
        RequireActive(expense);

        expense.Status = FinancialEntryStatus.Cancelled;
        expense.UpdatedAt = _dateTimeProvider.UtcNow;
        await _expenses.UpdateAsync(expense, cancellationToken);
        return ToDto(expense);
    }

    private void RequireNotFuture(DateOnly date)
    {
        var today = LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        if (date > today)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateExpenseRequest.Date)] = ["Date cannot be in the future."],
            });
        }
    }

    private static void RequireActive(Expense expense)
    {
        if (expense.Status != FinancialEntryStatus.Active)
        {
            throw new DomainException("This expense has been deleted and can no longer be edited.");
        }
    }

    private static IEnumerable<Expense> ApplySort(IEnumerable<Expense> expenses, ExpenseSortField sortBy, bool descending)
    {
        Func<Expense, object> keySelector = sortBy switch
        {
            ExpenseSortField.Amount => e => e.Amount,
            ExpenseSortField.CreatedAt => e => e.CreatedAt,
            ExpenseSortField.Merchant => e => e.Merchant ?? string.Empty,
            ExpenseSortField.Category => e => e.CategoryName,
            _ => e => e.Date,
        };

        return descending ? expenses.OrderByDescending(keySelector) : expenses.OrderBy(keySelector);
    }

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? Domain.Constants.CurrencyDefaults.DefaultCurrency : currency.Trim().ToUpperInvariant();

    private static ExpensePaymentMethod ParsePaymentMethod(string? value) =>
        string.IsNullOrWhiteSpace(value) ? ExpensePaymentMethod.Cash : Enum.Parse<ExpensePaymentMethod>(value, ignoreCase: true);

    private static List<string> NormalizeTags(List<string>? tags) =>
        (tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<Expense> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var expense = await _expenses.GetByIdAsync(id, cancellationToken);
        if (expense is null || expense.UserId != userId)
        {
            throw new NotFoundException("Expense", id);
        }

        return expense;
    }

    private static ExpenseDto ToDto(Expense expense) => new()
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
        IsRecurringGenerated = expense.RecurringExpenseId != null,
        Status = expense.Status.ToString(),
        CreatedAt = expense.CreatedAt,
        UpdatedAt = expense.UpdatedAt,
    };
}
