using KotoDibo.Application.Common;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BudgetDashboard.DTOs;
using KotoDibo.Application.Features.BudgetDashboard.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Constants;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using BudgetEntity = KotoDibo.Domain.Entities.Budget;

namespace KotoDibo.Application.Features.BudgetDashboard.Services;

// The "one call gets everything" entry point (Prompt §43): every number below is computed here,
// once, so the frontend never re-derives spent/remaining/percentage/variance itself. Every section
// respects the caller's resolved [From, To] window uniformly (Prompt §25) — there is no hidden
// second date range smuggled in from a Budget's own StartDate/EndDate; a matched Budget only
// supplies its planned/rollover *amounts*, which are then measured against actual spend within
// whatever window the caller asked for (so a "This Week" dashboard shows week-to-date pace against
// the monthly envelope, the same progress-bar-at-any-zoom pattern real budgeting apps use).
public class BudgetDashboardService : IBudgetDashboardService
{
    private const int UpcomingExpenseHorizonDays = 30;
    private const int TopListSize = 10;
    private const int RecentExpensesSize = 5;

    private readonly IRepository<Expense> _expenses;
    private readonly IRepository<BudgetEntity> _budgets;
    private readonly IRepository<BudgetCategoryAllocation> _allocations;
    private readonly IRepository<RecurringExpense> _recurringExpenses;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BudgetDashboardService(
        IRepository<Expense> expenses,
        IRepository<BudgetEntity> budgets,
        IRepository<BudgetCategoryAllocation> allocations,
        IRepository<RecurringExpense> recurringExpenses,
        IDateTimeProvider dateTimeProvider)
    {
        _expenses = expenses;
        _budgets = budgets;
        _allocations = allocations;
        _recurringExpenses = recurringExpenses;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<DashboardResponse> GetDashboardAsync(string userId, DashboardQuery query, CancellationToken cancellationToken = default)
    {
        var today = LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        var (from, to) = DateRangeResolver.Resolve(query.Preset, query.From, query.To, today);
        var currency = string.IsNullOrWhiteSpace(query.Currency) ? null : query.Currency.Trim().ToUpperInvariant();

        var expenses = await LoadExpensesAsync(userId, from, to, currency, cancellationToken);
        var totalSpent = expenses.Sum(e => e.Amount);

        var matchedBudget = await FindMatchedBudgetAsync(userId, query.BudgetId, from, to, cancellationToken);
        var budgetSummary = matchedBudget is null
            ? null
            : await ComputeBudgetSummaryAsync(matchedBudget, expenses, cancellationToken);

        var categoryBreakdown = BuildCategoryBreakdown(budgetSummary, totalSpent);
        var overspending = categoryBreakdown.Where(c => c.Status == nameof(BudgetCategoryStatus.Overspent)).ToList();

        var summary = new DashboardSummaryDto
        {
            TotalBudget = budgetSummary?.TotalAvailable ?? 0m,
            TotalAllocated = budgetSummary?.TotalPlanned ?? 0m,
            TotalSpent = totalSpent,
            TotalRemaining = budgetSummary?.TotalRemaining ?? 0m,
            TotalOverspent = budgetSummary?.TotalOverspent ?? 0m,
            BudgetUtilizationPercentage = budgetSummary?.UtilizationPercentage,
            ExpenseCount = expenses.Count,
            AverageExpense = expenses.Count > 0 ? Math.Round(totalSpent / expenses.Count, 2) : 0m,
        };

        var budgetDto = new DashboardBudgetDto
        {
            HasBudget = matchedBudget is not null,
            Id = matchedBudget?.Id,
            Name = matchedBudget?.Name,
            Status = matchedBudget?.Status.ToString(),
            Health = (budgetSummary?.Health ?? BudgetHealthStatus.NoBudget).ToString(),
        };

        var expensesSection = new DashboardExpensesDto
        {
            Count = expenses.Count,
            TotalAmount = totalSpent,
            AverageAmount = summary.AverageExpense,
            RecentExpenses = expenses
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.CreatedAt)
                .Take(RecentExpensesSize)
                .Select(ToExpenseDto)
                .ToList(),
        };

        var topCategories = BuildTopCategories(expenses, totalSpent);
        var topMerchants = BuildTopMerchants(expenses, totalSpent);
        var spendingTrend = await BuildSpendingTrendAsync(expenses, from, to);
        var budgetVsActual = await BuildBudgetVsActualAsync(userId, expenses, from, to, cancellationToken);
        var upcomingExpenses = await BuildUpcomingExpensesAsync(userId, today, cancellationToken);
        var comparison = query.ComparisonPeriod == DashboardComparisonPeriod.None
            ? null
            : await BuildComparisonAsync(userId, query, from, to, currency, summary.TotalSpent, summary.TotalBudget, cancellationToken);
        var insights = BuildInsights(expenses, categoryBreakdown, topCategories, totalSpent);

        return new DashboardResponse
        {
            Period = new DashboardPeriodDto { From = from, To = to, Preset = (query.Preset ?? DashboardPeriodPreset.Custom).ToString() },
            Summary = summary,
            Budget = budgetDto,
            Expenses = expensesSection,
            BudgetVsActual = budgetVsActual,
            CategoryBreakdown = categoryBreakdown,
            SpendingTrend = spendingTrend,
            TopCategories = topCategories,
            TopMerchants = topMerchants,
            Overspending = overspending,
            UpcomingExpenses = upcomingExpenses,
            Comparison = comparison,
            Insights = insights,
        };
    }

    private async Task<List<Expense>> LoadExpensesAsync(string userId, DateOnly from, DateOnly to, string? currency, CancellationToken cancellationToken)
    {
        var expenses = await _expenses.FindAsync(
            e => e.UserId == userId && e.Status == FinancialEntryStatus.Active && e.Date >= from && e.Date <= to,
            cancellationToken);

        var filtered = currency is null ? expenses : expenses.Where(e => e.Currency == currency);
        return filtered.ToList();
    }

    private async Task<BudgetEntity?> FindMatchedBudgetAsync(string userId, string? explicitBudgetId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (explicitBudgetId is not null)
        {
            var budget = await _budgets.GetByIdAsync(explicitBudgetId, cancellationToken);
            return budget is not null && budget.UserId == userId ? budget : null;
        }

        var overlapping = await _budgets.FindAsync(
            b => b.UserId == userId && b.StartDate <= to && b.EndDate >= from
                && (b.Status == BudgetStatus.Active || b.Status == BudgetStatus.Completed),
            cancellationToken);

        return overlapping
            .OrderBy(b => b.Status == BudgetStatus.Active ? 0 : 1)
            .ThenByDescending(b => b.StartDate)
            .FirstOrDefault();
    }

    private async Task<BudgetSummaryResult> ComputeBudgetSummaryAsync(BudgetEntity budget, IReadOnlyList<Expense> windowExpenses, CancellationToken cancellationToken)
    {
        var allocations = await _allocations.FindAsync(a => a.BudgetId == budget.Id, cancellationToken);
        var spentByCategory = windowExpenses.GroupBy(e => e.CategoryId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        var allocatedCategoryIds = allocations.Select(a => a.CategoryId).ToHashSet();
        var uncategorizedSpent = windowExpenses.Where(e => !allocatedCategoryIds.Contains(e.CategoryId)).Sum(e => e.Amount);

        var inputs = allocations.Select(a => new CategoryBudgetInput
        {
            CategoryAllocationId = a.Id,
            CategoryId = a.CategoryId,
            CategoryName = a.CategoryName,
            PlannedAmount = a.PlannedAmount,
            RolloverAmount = a.RolloverAmount,
            Spent = spentByCategory.GetValueOrDefault(a.CategoryId, 0m),
        }).ToList();

        return BudgetCalculator.Summarize(inputs, uncategorizedSpent);
    }

    private static List<CategoryBreakdownDto> BuildCategoryBreakdown(BudgetSummaryResult? budgetSummary, decimal totalSpent)
    {
        if (budgetSummary is null)
        {
            return [];
        }

        return budgetSummary.Categories.Select(c => new CategoryBreakdownDto
        {
            CategoryId = c.CategoryId,
            CategoryName = c.CategoryName,
            Budget = c.TotalAvailable,
            Spent = c.Spent,
            Remaining = c.Remaining,
            Variance = c.Variance,
            UtilizationPercentage = c.UsagePercentage,
            Status = c.Status.ToString(),
            PercentageOfTotalSpending = totalSpent > 0 ? Math.Round(c.Spent / totalSpent * 100m, 2) : null,
        }).ToList();
    }

    private static List<TopCategoryDto> BuildTopCategories(IReadOnlyList<Expense> expenses, decimal totalSpent)
    {
        return expenses
            .GroupBy(e => (e.CategoryId, e.CategoryName))
            .Select(g => new TopCategoryDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Amount = g.Sum(e => e.Amount),
                PercentageOfTotal = totalSpent > 0 ? Math.Round(g.Sum(e => e.Amount) / totalSpent * 100m, 2) : 0m,
            })
            .OrderByDescending(c => c.Amount)
            .Take(TopListSize)
            .ToList();
    }

    private static List<TopMerchantDto> BuildTopMerchants(IReadOnlyList<Expense> expenses, decimal totalSpent)
    {
        return expenses
            .Where(e => !string.IsNullOrWhiteSpace(e.Merchant))
            .GroupBy(e => e.Merchant!.Trim())
            .Select(g => new TopMerchantDto
            {
                Merchant = g.Key,
                Amount = g.Sum(e => e.Amount),
                TransactionCount = g.Count(),
                PercentageOfTotal = totalSpent > 0 ? Math.Round(g.Sum(e => e.Amount) / totalSpent * 100m, 2) : 0m,
            })
            .OrderByDescending(m => m.Amount)
            .Take(TopListSize)
            .ToList();
    }

    // Granularity scales with the requested span so a one-year query doesn't return 365 daily
    // points: <=31 days buckets by day, <=182 days by ISO week (Monday start), otherwise by month.
    private static Task<List<SpendingTrendPointDto>> BuildSpendingTrendAsync(IReadOnlyList<Expense> expenses, DateOnly from, DateOnly to)
    {
        var spanDays = to.DayNumber - from.DayNumber + 1;

        DateOnly BucketKey(DateOnly date) => spanDays switch
        {
            <= 31 => date,
            <= 182 => DateRangeResolver.StartOfWeek(date),
            _ => DateRangeResolver.StartOfMonth(date),
        };

        var buckets = expenses
            .GroupBy(e => BucketKey(e.Date))
            .Select(g => new SpendingTrendPointDto { Date = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderBy(p => p.Date)
            .ToList();

        return Task.FromResult(buckets);
    }

    // One point per calendar month the query window touches — the whole planned+rollover total of
    // every Active/Completed budget overlapping that month is attributed to it (not day-prorated;
    // budgets in this app are effectively always calendar-aligned, so the simpler attribution reads
    // the same in practice and avoids a proration model nothing here needs yet).
    private async Task<List<BudgetVsActualPointDto>> BuildBudgetVsActualAsync(string userId, IReadOnlyList<Expense> windowExpenses, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var overlappingBudgets = await _budgets.FindAsync(
            b => b.UserId == userId && b.StartDate <= to && b.EndDate >= from
                && (b.Status == BudgetStatus.Active || b.Status == BudgetStatus.Completed),
            cancellationToken);

        var budgetTotals = new Dictionary<string, decimal>();
        foreach (var budget in overlappingBudgets)
        {
            var allocations = await _allocations.FindAsync(a => a.BudgetId == budget.Id, cancellationToken);
            budgetTotals[budget.Id] = allocations.Sum(a => a.PlannedAmount + a.RolloverAmount);
        }

        var points = new List<BudgetVsActualPointDto>();
        var cursor = DateRangeResolver.StartOfMonth(from);
        while (cursor <= to)
        {
            var monthEnd = DateRangeResolver.EndOfMonth(cursor);
            var bucketFrom = cursor < from ? from : cursor;
            var bucketTo = monthEnd > to ? to : monthEnd;

            var budgetTotal = overlappingBudgets
                .Where(b => b.StartDate <= monthEnd && b.EndDate >= cursor)
                .Sum(b => budgetTotals.GetValueOrDefault(b.Id, 0m));
            var actual = windowExpenses.Where(e => e.Date >= bucketFrom && e.Date <= bucketTo).Sum(e => e.Amount);

            points.Add(new BudgetVsActualPointDto
            {
                Label = cursor.ToString("yyyy-MM"),
                Budget = budgetTotal,
                Actual = actual,
            });

            cursor = cursor.AddMonths(1);
        }

        return points;
    }

    private async Task<List<UpcomingExpenseDto>> BuildUpcomingExpensesAsync(string userId, DateOnly today, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(UpcomingExpenseHorizonDays);
        var recurring = await _recurringExpenses.FindAsync(
            r => r.UserId == userId && r.IsActive && r.NextOccurrenceDate >= today && r.NextOccurrenceDate <= horizon,
            cancellationToken);

        return recurring
            .OrderBy(r => r.NextOccurrenceDate)
            .Take(TopListSize)
            .Select(r => new UpcomingExpenseDto
            {
                RecurringExpenseId = r.Id,
                Description = r.Description,
                Merchant = r.Merchant,
                CategoryName = r.CategoryName,
                Amount = r.Amount,
                NextOccurrenceDate = r.NextOccurrenceDate,
                DaysUntilDue = r.NextOccurrenceDate.DayNumber - today.DayNumber,
            })
            .ToList();
    }

    private async Task<DashboardComparisonDto> BuildComparisonAsync(
        string userId,
        DashboardQuery query,
        DateOnly from,
        DateOnly to,
        string? currency,
        decimal currentSpending,
        decimal currentBudget,
        CancellationToken cancellationToken)
    {
        var (previousFrom, previousTo) = query.ComparisonPeriod == DashboardComparisonPeriod.SamePeriodLastYear
            ? DateRangeResolver.SamePeriodLastYear(from, to)
            : DateRangeResolver.PreviousPeriod(from, to);

        var previousExpenses = await LoadExpensesAsync(userId, previousFrom, previousTo, currency, cancellationToken);
        var previousSpending = previousExpenses.Sum(e => e.Amount);

        var previousBudgetEntity = await FindMatchedBudgetAsync(userId, query.BudgetId, previousFrom, previousTo, cancellationToken);
        var previousBudget = previousBudgetEntity is null
            ? 0m
            : (await ComputeBudgetSummaryAsync(previousBudgetEntity, previousExpenses, cancellationToken)).TotalAvailable;

        var spendingChange = currentSpending - previousSpending;
        decimal? spendingChangePercentage = previousSpending != 0 ? Math.Round(spendingChange / previousSpending * 100m, 2) : null;

        var trend = DetermineTrend(spendingChangePercentage, spendingChange);

        return new DashboardComparisonDto
        {
            PreviousFrom = previousFrom,
            PreviousTo = previousTo,
            CurrentSpending = currentSpending,
            PreviousSpending = previousSpending,
            SpendingChange = spendingChange,
            SpendingChangePercentage = spendingChangePercentage,
            CurrentBudget = currentBudget,
            PreviousBudget = previousBudget,
            BudgetChange = currentBudget - previousBudget,
            BudgetChangePercentage = previousBudget != 0 ? Math.Round((currentBudget - previousBudget) / previousBudget * 100m, 2) : null,
            Trend = trend,
        };
    }

    private static string DetermineTrend(decimal? changePercentage, decimal rawChange)
    {
        if (changePercentage is { } pct)
        {
            if (Math.Abs(pct) <= BudgetThresholds.StableSpendingChangePercentage)
            {
                return "Stable";
            }

            return pct > 0 ? "Increased" : "Decreased";
        }

        return rawChange switch
        {
            > 0 => "Increased",
            < 0 => "Decreased",
            _ => "Stable",
        };
    }

    private static DashboardInsightsDto BuildInsights(
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<CategoryBreakdownDto> categoryBreakdown,
        IReadOnlyList<TopCategoryDto> topCategories,
        decimal totalSpent)
    {
        var highestExpense = expenses.OrderByDescending(e => e.Amount).FirstOrDefault();
        var mostFrequentCategory = expenses
            .GroupBy(e => e.CategoryName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var fixedTotal = expenses.Where(e => e.RecurringExpenseId != null).Sum(e => e.Amount);

        return new DashboardInsightsDto
        {
            HighestSpendingCategory = topCategories.FirstOrDefault()?.CategoryName,
            MostFrequentCategory = mostFrequentCategory?.Key,
            HighestExpenseAmount = highestExpense?.Amount,
            HighestExpenseDescription = highestExpense?.Description,
            AverageExpense = expenses.Count > 0 ? Math.Round(totalSpent / expenses.Count, 2) : 0m,
            OverspendingCategoriesCount = categoryBreakdown.Count(c => c.Status == nameof(BudgetCategoryStatus.Overspent)),
            CategoriesApproachingLimit = categoryBreakdown.Where(c => c.Status == nameof(BudgetCategoryStatus.Warning)).Select(c => c.CategoryName).ToList(),
            CategoriesSignificantlyUnderBudget = categoryBreakdown
                .Where(c => c.UtilizationPercentage is { } pct && pct < BudgetThresholds.CategoryUnderUsedPercentage)
                .Select(c => c.CategoryName)
                .ToList(),
            RecurringExpensesTotal = fixedTotal,
            FixedExpensesTotal = fixedTotal,
            VariableExpensesTotal = totalSpent - fixedTotal,
        };
    }

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
        IsRecurringGenerated = expense.RecurringExpenseId != null,
        Status = expense.Status.ToString(),
        CreatedAt = expense.CreatedAt,
        UpdatedAt = expense.UpdatedAt,
    };
}
