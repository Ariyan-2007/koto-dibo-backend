using KotoDibo.Domain.Constants;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Calculations;

public record CategoryBudgetInput
{
    public string CategoryAllocationId { get; init; } = string.Empty;
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public decimal RolloverAmount { get; init; }
    public decimal Spent { get; init; }
}

public record CategoryBudgetResult
{
    public string CategoryAllocationId { get; init; } = string.Empty;
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public decimal RolloverAmount { get; init; }
    public decimal TotalAvailable { get; init; }
    public decimal Spent { get; init; }
    public decimal Remaining { get; init; }
    public decimal Variance { get; init; }

    // Null rather than an arbitrary number when TotalAvailable is 0 — there is no meaningful
    // percentage of zero, and forcing one (0 or 100) would misrepresent an unbudgeted category.
    public decimal? UsagePercentage { get; init; }
    public BudgetCategoryStatus Status { get; init; }
}

public record BudgetSummaryResult
{
    public decimal TotalPlanned { get; init; }
    public decimal TotalRollover { get; init; }
    public decimal TotalAvailable { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal TotalRemaining { get; init; }
    public decimal TotalOverspent { get; init; }
    public decimal? UtilizationPercentage { get; init; }
    public BudgetHealthStatus Health { get; init; }
    public IReadOnlyList<CategoryBudgetResult> Categories { get; init; } = [];
}

// Pure budget-vs-actual math shared by the Budget detail endpoint and the dashboard — every
// planned/spent/remaining/variance/usage% number the API returns for a budget flows through here
// exactly once, so the two surfaces can never silently disagree.
public static class BudgetCalculator
{
    public static CategoryBudgetResult EvaluateCategory(CategoryBudgetInput input)
    {
        var totalAvailable = input.PlannedAmount + input.RolloverAmount;
        var remaining = totalAvailable - input.Spent;
        decimal? usagePercentage = totalAvailable != 0
            ? Math.Round(input.Spent / totalAvailable * 100m, 2)
            : null;

        var status = DetermineCategoryStatus(totalAvailable, usagePercentage);

        return new CategoryBudgetResult
        {
            CategoryAllocationId = input.CategoryAllocationId,
            CategoryId = input.CategoryId,
            CategoryName = input.CategoryName,
            PlannedAmount = input.PlannedAmount,
            RolloverAmount = input.RolloverAmount,
            TotalAvailable = totalAvailable,
            Spent = input.Spent,
            Remaining = remaining,
            Variance = remaining,
            UsagePercentage = usagePercentage,
            Status = status,
        };
    }

    public static BudgetSummaryResult Summarize(IReadOnlyList<CategoryBudgetInput> categoryInputs, decimal uncategorizedSpent)
    {
        var categories = categoryInputs.Select(EvaluateCategory).ToList();

        var totalPlanned = categoryInputs.Sum(c => c.PlannedAmount);
        var totalRollover = categoryInputs.Sum(c => c.RolloverAmount);
        var totalAvailable = totalPlanned + totalRollover;
        var totalSpent = categoryInputs.Sum(c => c.Spent) + uncategorizedSpent;
        var totalRemaining = totalAvailable - totalSpent;
        var totalOverspent = categories.Where(c => c.Remaining < 0).Sum(c => -c.Remaining);

        decimal? utilization = totalAvailable != 0
            ? Math.Round(totalSpent / totalAvailable * 100m, 2)
            : null;

        var overspentCount = categories.Count(c => c.Status == BudgetCategoryStatus.Overspent);
        var health = DetermineHealth(totalAvailable, utilization, overspentCount);

        return new BudgetSummaryResult
        {
            TotalPlanned = totalPlanned,
            TotalRollover = totalRollover,
            TotalAvailable = totalAvailable,
            TotalSpent = totalSpent,
            TotalRemaining = totalRemaining,
            TotalOverspent = totalOverspent,
            UtilizationPercentage = utilization,
            Health = health,
            Categories = categories,
        };
    }

    private static BudgetCategoryStatus DetermineCategoryStatus(decimal totalAvailable, decimal? usagePercentage)
    {
        if (totalAvailable == 0)
        {
            return BudgetCategoryStatus.NoBudget;
        }

        return usagePercentage switch
        {
            > 100m => BudgetCategoryStatus.Overspent,
            >= BudgetThresholds.CategoryWarningPercentage => BudgetCategoryStatus.Warning,
            _ => BudgetCategoryStatus.OnTrack,
        };
    }

    private static BudgetHealthStatus DetermineHealth(decimal totalAvailable, decimal? utilizationPercentage, int overspentCategoryCount)
    {
        if (totalAvailable == 0)
        {
            return BudgetHealthStatus.NoBudget;
        }

        if (utilizationPercentage >= BudgetThresholds.OverallCriticalPercentage
            || overspentCategoryCount >= BudgetThresholds.CriticalOverspentCategoryCount)
        {
            return BudgetHealthStatus.Critical;
        }

        if (utilizationPercentage > 100m || overspentCategoryCount > 0)
        {
            return BudgetHealthStatus.Overspending;
        }

        if (utilizationPercentage >= BudgetThresholds.OverallWarningPercentage)
        {
            return BudgetHealthStatus.Warning;
        }

        return BudgetHealthStatus.Healthy;
    }
}
