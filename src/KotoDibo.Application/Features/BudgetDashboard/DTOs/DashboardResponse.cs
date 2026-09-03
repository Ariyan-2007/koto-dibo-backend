using KotoDibo.Application.Features.Expenses.DTOs;

namespace KotoDibo.Application.Features.BudgetDashboard.DTOs;

public record DashboardPeriodDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string Preset { get; init; } = string.Empty;
}

public record DashboardSummaryDto
{
    public decimal TotalBudget { get; init; }
    public decimal TotalAllocated { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal TotalRemaining { get; init; }
    public decimal TotalOverspent { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
    public int ExpenseCount { get; init; }
    public decimal AverageExpense { get; init; }
}

public record DashboardBudgetDto
{
    public bool HasBudget { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Status { get; init; }
    public string Health { get; init; } = string.Empty;
}

public record DashboardExpensesDto
{
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AverageAmount { get; init; }
    public IReadOnlyList<ExpenseDto> RecentExpenses { get; init; } = [];
}

public record BudgetVsActualPointDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Budget { get; init; }
    public decimal Actual { get; init; }
}

public record CategoryBreakdownDto
{
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal Budget { get; init; }
    public decimal Spent { get; init; }
    public decimal Remaining { get; init; }
    public decimal Variance { get; init; }
    public decimal? UtilizationPercentage { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? PercentageOfTotalSpending { get; init; }
}

public record SpendingTrendPointDto
{
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
}

public record TopCategoryDto
{
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal PercentageOfTotal { get; init; }
}

public record TopMerchantDto
{
    public string Merchant { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int TransactionCount { get; init; }
    public decimal PercentageOfTotal { get; init; }
}

public record UpcomingExpenseDto
{
    public string RecurringExpenseId { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Merchant { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateOnly NextOccurrenceDate { get; init; }
    public int DaysUntilDue { get; init; }
}

public record DashboardComparisonDto
{
    public DateOnly PreviousFrom { get; init; }
    public DateOnly PreviousTo { get; init; }
    public decimal CurrentSpending { get; init; }
    public decimal PreviousSpending { get; init; }
    public decimal SpendingChange { get; init; }
    public decimal? SpendingChangePercentage { get; init; }
    public decimal CurrentBudget { get; init; }
    public decimal PreviousBudget { get; init; }
    public decimal BudgetChange { get; init; }
    public decimal? BudgetChangePercentage { get; init; }

    // Increased / Decreased / Stable — "Stable" absorbs anything within
    // DashboardThresholds.StableSpendingChangePercentage of 0% so tiny noise doesn't read as a trend.
    public string Trend { get; init; } = string.Empty;
}

public record DashboardInsightsDto
{
    public string? HighestSpendingCategory { get; init; }
    public string? MostFrequentCategory { get; init; }
    public decimal? HighestExpenseAmount { get; init; }
    public string? HighestExpenseDescription { get; init; }
    public decimal AverageExpense { get; init; }
    public int OverspendingCategoriesCount { get; init; }
    public IReadOnlyList<string> CategoriesApproachingLimit { get; init; } = [];
    public IReadOnlyList<string> CategoriesSignificantlyUnderBudget { get; init; } = [];
    public decimal RecurringExpensesTotal { get; init; }
    public decimal FixedExpensesTotal { get; init; }
    public decimal VariableExpensesTotal { get; init; }
}

public record DashboardResponse
{
    public DashboardPeriodDto Period { get; init; } = new();
    public DashboardSummaryDto Summary { get; init; } = new();
    public DashboardBudgetDto Budget { get; init; } = new();
    public DashboardExpensesDto Expenses { get; init; } = new();
    public IReadOnlyList<BudgetVsActualPointDto> BudgetVsActual { get; init; } = [];
    public IReadOnlyList<CategoryBreakdownDto> CategoryBreakdown { get; init; } = [];
    public IReadOnlyList<SpendingTrendPointDto> SpendingTrend { get; init; } = [];
    public IReadOnlyList<TopCategoryDto> TopCategories { get; init; } = [];
    public IReadOnlyList<TopMerchantDto> TopMerchants { get; init; } = [];
    public IReadOnlyList<CategoryBreakdownDto> Overspending { get; init; } = [];
    public IReadOnlyList<UpcomingExpenseDto> UpcomingExpenses { get; init; } = [];
    public DashboardComparisonDto? Comparison { get; init; }
    public DashboardInsightsDto Insights { get; init; } = new();
}
