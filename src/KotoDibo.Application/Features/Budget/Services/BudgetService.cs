using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using BudgetEntity = KotoDibo.Domain.Entities.Budget;

namespace KotoDibo.Application.Features.Budget.Services;

public class BudgetService : IBudgetService
{
    // Draft can go Active or straight to Archived (abandoned before ever tracking spend);
    // Active can Complete or be Archived early; Completed can only be Archived. Nothing moves
    // backward — a Completed/Archived budget's numbers are meant to stay a settled historical record.
    private static readonly Dictionary<BudgetStatus, BudgetStatus[]> AllowedTransitions = new()
    {
        [BudgetStatus.Draft] = [BudgetStatus.Active, BudgetStatus.Archived],
        [BudgetStatus.Active] = [BudgetStatus.Completed, BudgetStatus.Archived],
        [BudgetStatus.Completed] = [BudgetStatus.Archived],
        [BudgetStatus.Archived] = [],
    };

    private readonly IRepository<BudgetEntity> _budgets;
    private readonly IRepository<BudgetCategoryAllocation> _allocations;
    private readonly IRepository<BudgetAdjustment> _adjustments;
    private readonly IRepository<Expense> _expenses;
    private readonly IExpenseCategoryService _categories;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateBudgetRequest> _createValidator;
    private readonly IValidator<UpdateBudgetRequest> _updateValidator;
    private readonly IValidator<AddBudgetCategoryRequest> _addCategoryValidator;
    private readonly IValidator<AdjustBudgetCategoryRequest> _adjustCategoryValidator;
    private readonly IValidator<TransferBudgetCategoryRequest> _transferValidator;
    private readonly IValidator<RolloverBudgetRequest> _rolloverValidator;

    public BudgetService(
        IRepository<BudgetEntity> budgets,
        IRepository<BudgetCategoryAllocation> allocations,
        IRepository<BudgetAdjustment> adjustments,
        IRepository<Expense> expenses,
        IExpenseCategoryService categories,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateBudgetRequest> createValidator,
        IValidator<UpdateBudgetRequest> updateValidator,
        IValidator<AddBudgetCategoryRequest> addCategoryValidator,
        IValidator<AdjustBudgetCategoryRequest> adjustCategoryValidator,
        IValidator<TransferBudgetCategoryRequest> transferValidator,
        IValidator<RolloverBudgetRequest> rolloverValidator)
    {
        _budgets = budgets;
        _allocations = allocations;
        _adjustments = adjustments;
        _expenses = expenses;
        _categories = categories;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addCategoryValidator = addCategoryValidator;
        _adjustCategoryValidator = adjustCategoryValidator;
        _transferValidator = transferValidator;
        _rolloverValidator = rolloverValidator;
    }

    public async Task<BudgetDto> CreateAsync(string userId, CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var periodType = Enum.Parse<BudgetPeriodType>(request.PeriodType, ignoreCase: true);
        var endDate = request.EndDate ?? ComputePeriodEnd(request.StartDate, periodType);

        var now = _dateTimeProvider.UtcNow;
        var budget = new BudgetEntity
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? Domain.Constants.CurrencyDefaults.DefaultCurrency : request.Currency.Trim().ToUpperInvariant(),
            PeriodType = periodType,
            StartDate = request.StartDate,
            EndDate = endDate,
            Status = BudgetStatus.Draft,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _budgets.AddAsync(budget, cancellationToken);

        foreach (var categoryInput in request.Categories ?? [])
        {
            await CreateAllocationAsync(userId, budget, categoryInput.CategoryId, categoryInput.PlannedAmount, categoryInput.RolloverEnabled, categoryInput.Notes, now, cancellationToken);
        }

        return await BuildBudgetDtoAsync(budget, cancellationToken);
    }

    public async Task<BudgetDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var budget = await GetOwnedAsync(userId, id, cancellationToken);
        return await BuildBudgetDtoAsync(budget, cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetSummaryDto>> GetAllAsync(string userId, string? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;

        var budgets = await _budgets.FindAsync(
            b => b.UserId == userId && b.EndDate >= effectiveFrom && b.StartDate <= effectiveTo,
            cancellationToken);

        IEnumerable<BudgetEntity> filtered = budgets;
        if (status is not null)
        {
            var parsedStatus = Enum.Parse<BudgetStatus>(status, ignoreCase: true);
            filtered = filtered.Where(b => b.Status == parsedStatus);
        }

        var ordered = filtered.OrderByDescending(b => b.StartDate).ToList();
        var summaries = new List<BudgetSummaryDto>();
        foreach (var budget in ordered)
        {
            var dto = await BuildBudgetDtoAsync(budget, cancellationToken);
            summaries.Add(new BudgetSummaryDto
            {
                Id = dto.Id,
                Name = dto.Name,
                Currency = dto.Currency,
                PeriodType = dto.PeriodType,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Status = dto.Status,
                TotalPlanned = dto.TotalPlanned,
                TotalAvailable = dto.TotalAvailable,
                TotalSpent = dto.TotalSpent,
                TotalRemaining = dto.TotalRemaining,
                UtilizationPercentage = dto.UtilizationPercentage,
                Health = dto.Health,
            });
        }

        return summaries;
    }

    public async Task<BudgetDto> UpdateAsync(string userId, string id, UpdateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var budget = await GetOwnedAsync(userId, id, cancellationToken);

        if (request.Name is not null)
        {
            budget.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            budget.Description = request.Description.Trim();
        }

        if (request.Notes is not null)
        {
            budget.Notes = request.Notes.Trim();
        }

        if (request.Status is not null)
        {
            var newStatus = Enum.Parse<BudgetStatus>(request.Status, ignoreCase: true);
            if (newStatus != budget.Status && !AllowedTransitions[budget.Status].Contains(newStatus))
            {
                throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.Status)] = [$"Cannot move a budget from '{budget.Status}' to '{newStatus}'."],
                });
            }

            budget.Status = newStatus;
        }

        budget.UpdatedAt = _dateTimeProvider.UtcNow;
        await _budgets.UpdateAsync(budget, cancellationToken);
        return await BuildBudgetDtoAsync(budget, cancellationToken);
    }

    public async Task<BudgetDto> AddCategoryAsync(string userId, string budgetId, AddBudgetCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await _addCategoryValidator.ValidateAndThrowAsync(request, cancellationToken);

        var budget = await GetOwnedAsync(userId, budgetId, cancellationToken);
        RequireEditable(budget);

        var existing = await _allocations.FindOneAsync(a => a.BudgetId == budgetId && a.CategoryId == request.CategoryId, cancellationToken);
        if (existing is not null)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.CategoryId)] = ["This budget already has an allocation for this category."],
            });
        }

        await CreateAllocationAsync(userId, budget, request.CategoryId, request.PlannedAmount, request.RolloverEnabled, request.Notes, _dateTimeProvider.UtcNow, cancellationToken);
        return await BuildBudgetDtoAsync(budget, cancellationToken);
    }

    public async Task<BudgetDto> AdjustCategoryAsync(string userId, string budgetId, string allocationId, AdjustBudgetCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await _adjustCategoryValidator.ValidateAndThrowAsync(request, cancellationToken);

        var budget = await GetOwnedAsync(userId, budgetId, cancellationToken);
        RequireEditable(budget);
        var allocation = await GetAllocationAsync(budgetId, allocationId, cancellationToken);

        var newPlanned = allocation.PlannedAmount + request.Delta;
        if (newPlanned < 0)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Delta)] = ["This adjustment would make PlannedAmount negative."],
            });
        }

        var now = _dateTimeProvider.UtcNow;
        allocation.PlannedAmount = newPlanned;
        allocation.UpdatedAt = now;
        await _allocations.UpdateAsync(allocation, cancellationToken);

        await _adjustments.AddAsync(new BudgetAdjustment
        {
            BudgetId = budgetId,
            BudgetCategoryAllocationId = allocation.Id,
            UserId = userId,
            Type = request.Delta > 0 ? BudgetAdjustmentType.Increase : BudgetAdjustmentType.Decrease,
            Amount = request.Delta,
            BalanceAfter = allocation.PlannedAmount + allocation.RolloverAmount,
            Reason = request.Reason?.Trim(),
            CreatedAt = now,
        }, cancellationToken);

        return await BuildBudgetDtoAsync(budget, cancellationToken);
    }

    public async Task<BudgetDto> TransferCategoryAsync(string userId, string budgetId, string fromAllocationId, TransferBudgetCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await _transferValidator.ValidateAndThrowAsync(request, cancellationToken);

        var budget = await GetOwnedAsync(userId, budgetId, cancellationToken);
        RequireEditable(budget);

        if (fromAllocationId == request.ToCategoryAllocationId)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.ToCategoryAllocationId)] = ["Cannot transfer a category allocation into itself."],
            });
        }

        var from = await GetAllocationAsync(budgetId, fromAllocationId, cancellationToken);
        var to = await GetAllocationAsync(budgetId, request.ToCategoryAllocationId, cancellationToken);

        if (from.PlannedAmount < request.Amount)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Amount)] = ["Transfer amount exceeds the source category's planned amount."],
            });
        }

        var now = _dateTimeProvider.UtcNow;
        from.PlannedAmount -= request.Amount;
        from.UpdatedAt = now;
        to.PlannedAmount += request.Amount;
        to.UpdatedAt = now;

        await _allocations.UpdateAsync(from, cancellationToken);
        await _allocations.UpdateAsync(to, cancellationToken);

        var reason = request.Reason?.Trim();
        await _adjustments.AddAsync(new BudgetAdjustment
        {
            BudgetId = budgetId,
            BudgetCategoryAllocationId = from.Id,
            UserId = userId,
            Type = BudgetAdjustmentType.TransferOut,
            Amount = -request.Amount,
            BalanceAfter = from.PlannedAmount + from.RolloverAmount,
            RelatedCategoryAllocationId = to.Id,
            Reason = reason,
            CreatedAt = now,
        }, cancellationToken);
        await _adjustments.AddAsync(new BudgetAdjustment
        {
            BudgetId = budgetId,
            BudgetCategoryAllocationId = to.Id,
            UserId = userId,
            Type = BudgetAdjustmentType.TransferIn,
            Amount = request.Amount,
            BalanceAfter = to.PlannedAmount + to.RolloverAmount,
            RelatedCategoryAllocationId = from.Id,
            Reason = reason,
            CreatedAt = now,
        }, cancellationToken);

        return await BuildBudgetDtoAsync(budget, cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetAdjustmentDto>> GetAdjustmentHistoryAsync(string userId, string budgetId, string allocationId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(userId, budgetId, cancellationToken);
        await GetAllocationAsync(budgetId, allocationId, cancellationToken);

        var adjustments = await _adjustments.FindAsync(a => a.BudgetCategoryAllocationId == allocationId, cancellationToken);
        return adjustments
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new BudgetAdjustmentDto
            {
                Id = a.Id,
                BudgetCategoryAllocationId = a.BudgetCategoryAllocationId,
                Type = a.Type.ToString(),
                Amount = a.Amount,
                BalanceAfter = a.BalanceAfter,
                RelatedCategoryAllocationId = a.RelatedCategoryAllocationId,
                Reason = a.Reason,
                CreatedAt = a.CreatedAt,
            })
            .ToList();
    }

    public async Task<BudgetDto> RolloverAsync(string userId, string budgetId, RolloverBudgetRequest request, CancellationToken cancellationToken = default)
    {
        await _rolloverValidator.ValidateAndThrowAsync(request, cancellationToken);

        var current = await GetOwnedAsync(userId, budgetId, cancellationToken);
        var allocations = await _allocations.FindAsync(a => a.BudgetId == budgetId, cancellationToken);
        var summary = await BuildBudgetSummaryAsync(current, allocations, cancellationToken);

        DateOnly newStart;
        DateOnly newEnd;
        if (request.StartDate is not null)
        {
            newStart = request.StartDate.Value;
            newEnd = request.EndDate ?? (current.PeriodType == BudgetPeriodType.Custom
                ? throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.EndDate)] = ["EndDate is required when rolling over a Custom-period budget."],
                })
                : ComputePeriodEnd(newStart, current.PeriodType));
        }
        else
        {
            newStart = current.EndDate.AddDays(1);
            newEnd = current.PeriodType == BudgetPeriodType.Custom
                ? throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.StartDate)] = ["StartDate and EndDate are required when rolling over a Custom-period budget."],
                })
                : ComputePeriodEnd(newStart, current.PeriodType);
        }

        var now = _dateTimeProvider.UtcNow;
        var next = new BudgetEntity
        {
            UserId = userId,
            Name = request.Name?.Trim() ?? current.Name,
            Description = current.Description,
            Currency = current.Currency,
            PeriodType = current.PeriodType,
            StartDate = newStart,
            EndDate = newEnd,
            Status = BudgetStatus.Draft,
            Notes = current.Notes,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _budgets.AddAsync(next, cancellationToken);

        foreach (var categoryResult in summary.Categories)
        {
            var sourceAllocation = allocations.First(a => a.Id == categoryResult.CategoryAllocationId);
            var rolloverAmount = sourceAllocation.RolloverEnabled ? categoryResult.Remaining : 0m;

            var newAllocation = new BudgetCategoryAllocation
            {
                BudgetId = next.Id,
                UserId = userId,
                CategoryId = sourceAllocation.CategoryId,
                CategoryName = sourceAllocation.CategoryName,
                PlannedAmount = sourceAllocation.PlannedAmount,
                RolloverEnabled = sourceAllocation.RolloverEnabled,
                RolloverAmount = rolloverAmount,
                Notes = sourceAllocation.Notes,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _allocations.AddAsync(newAllocation, cancellationToken);

            await _adjustments.AddAsync(new BudgetAdjustment
            {
                BudgetId = next.Id,
                BudgetCategoryAllocationId = newAllocation.Id,
                UserId = userId,
                Type = BudgetAdjustmentType.Initial,
                Amount = newAllocation.PlannedAmount,
                BalanceAfter = newAllocation.PlannedAmount,
                CreatedAt = now,
            }, cancellationToken);

            if (rolloverAmount != 0)
            {
                await _adjustments.AddAsync(new BudgetAdjustment
                {
                    BudgetId = next.Id,
                    BudgetCategoryAllocationId = newAllocation.Id,
                    UserId = userId,
                    Type = BudgetAdjustmentType.Rollover,
                    Amount = rolloverAmount,
                    BalanceAfter = newAllocation.PlannedAmount + newAllocation.RolloverAmount,
                    Reason = $"Rolled over from budget {current.Id} ({current.Name}).",
                    CreatedAt = now,
                }, cancellationToken);
            }
        }

        return await BuildBudgetDtoAsync(next, cancellationToken);
    }

    private async Task CreateAllocationAsync(string userId, BudgetEntity budget, string categoryId, decimal plannedAmount, bool rolloverEnabled, string? notes, DateTime now, CancellationToken cancellationToken)
    {
        var category = await _categories.RequireVisibleAsync(userId, categoryId, cancellationToken);

        var allocation = new BudgetCategoryAllocation
        {
            BudgetId = budget.Id,
            UserId = userId,
            CategoryId = category.Id,
            CategoryName = category.Name,
            PlannedAmount = plannedAmount,
            RolloverEnabled = rolloverEnabled,
            RolloverAmount = 0m,
            Notes = notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _allocations.AddAsync(allocation, cancellationToken);

        await _adjustments.AddAsync(new BudgetAdjustment
        {
            BudgetId = budget.Id,
            BudgetCategoryAllocationId = allocation.Id,
            UserId = userId,
            Type = BudgetAdjustmentType.Initial,
            Amount = plannedAmount,
            BalanceAfter = plannedAmount,
            CreatedAt = now,
        }, cancellationToken);
    }

    private async Task<BudgetDto> BuildBudgetDtoAsync(BudgetEntity budget, CancellationToken cancellationToken)
    {
        var allocations = await _allocations.FindAsync(a => a.BudgetId == budget.Id, cancellationToken);
        var summary = await BuildBudgetSummaryAsync(budget, allocations, cancellationToken);

        return new BudgetDto
        {
            Id = budget.Id,
            Name = budget.Name,
            Description = budget.Description,
            Currency = budget.Currency,
            PeriodType = budget.PeriodType.ToString(),
            StartDate = budget.StartDate,
            EndDate = budget.EndDate,
            Status = budget.Status.ToString(),
            Notes = budget.Notes,
            TotalPlanned = summary.TotalPlanned,
            TotalRollover = summary.TotalRollover,
            TotalAvailable = summary.TotalAvailable,
            TotalSpent = summary.TotalSpent,
            TotalRemaining = summary.TotalRemaining,
            TotalOverspent = summary.TotalOverspent,
            UtilizationPercentage = summary.UtilizationPercentage,
            Health = summary.Health.ToString(),
            Categories = summary.Categories.Select(c => new BudgetCategoryDto
            {
                Id = c.CategoryAllocationId,
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                PlannedAmount = c.PlannedAmount,
                RolloverEnabled = allocations.First(a => a.Id == c.CategoryAllocationId).RolloverEnabled,
                RolloverAmount = c.RolloverAmount,
                TotalAvailable = c.TotalAvailable,
                Spent = c.Spent,
                Remaining = c.Remaining,
                Variance = c.Variance,
                UsagePercentage = c.UsagePercentage,
                Status = c.Status.ToString(),
                Notes = allocations.First(a => a.Id == c.CategoryAllocationId).Notes,
            }).ToList(),
            CreatedAt = budget.CreatedAt,
            UpdatedAt = budget.UpdatedAt,
        };
    }

    // Computes live planned/spent/remaining/variance/usage% for every category allocation plus the
    // whole-budget summary — the one place Expense data actually gets joined against a Budget's
    // allocations, so the detail endpoint, the list endpoint, and rollover all see identical numbers.
    private async Task<BudgetSummaryResult> BuildBudgetSummaryAsync(BudgetEntity budget, IReadOnlyList<BudgetCategoryAllocation> allocations, CancellationToken cancellationToken)
    {
        var expenses = await _expenses.FindAsync(
            e => e.UserId == budget.UserId
                && e.Status == FinancialEntryStatus.Active
                && e.Date >= budget.StartDate && e.Date <= budget.EndDate,
            cancellationToken);

        var spentByCategory = expenses
            .GroupBy(e => e.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var allocatedCategoryIds = allocations.Select(a => a.CategoryId).ToHashSet();
        var uncategorizedSpent = expenses.Where(e => !allocatedCategoryIds.Contains(e.CategoryId)).Sum(e => e.Amount);

        var categoryInputs = allocations.Select(a => new CategoryBudgetInput
        {
            CategoryAllocationId = a.Id,
            CategoryId = a.CategoryId,
            CategoryName = a.CategoryName,
            PlannedAmount = a.PlannedAmount,
            RolloverAmount = a.RolloverAmount,
            Spent = spentByCategory.GetValueOrDefault(a.CategoryId, 0m),
        }).ToList();

        return BudgetCalculator.Summarize(categoryInputs, uncategorizedSpent);
    }

    private static DateOnly ComputePeriodEnd(DateOnly startDate, BudgetPeriodType periodType) => periodType switch
    {
        BudgetPeriodType.Weekly => startDate.AddDays(6),
        BudgetPeriodType.Monthly => startDate.AddMonths(1).AddDays(-1),
        BudgetPeriodType.Yearly => startDate.AddYears(1).AddDays(-1),
        _ => throw new DomainException("EndDate must be provided explicitly for a Custom-period budget."),
    };

    private static void RequireEditable(BudgetEntity budget)
    {
        if (budget.Status == BudgetStatus.Archived)
        {
            throw new DomainException("This budget has been archived and can no longer be edited.");
        }
    }

    private async Task<BudgetEntity> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var budget = await _budgets.GetByIdAsync(id, cancellationToken);
        if (budget is null || budget.UserId != userId)
        {
            throw new NotFoundException("Budget", id);
        }

        return budget;
    }

    private async Task<BudgetCategoryAllocation> GetAllocationAsync(string budgetId, string allocationId, CancellationToken cancellationToken)
    {
        var allocation = await _allocations.GetByIdAsync(allocationId, cancellationToken);
        if (allocation is null || allocation.BudgetId != budgetId)
        {
            throw new NotFoundException("BudgetCategoryAllocation", allocationId);
        }

        return allocation;
    }
}
