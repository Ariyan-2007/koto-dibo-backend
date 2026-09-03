using FluentAssertions;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Constants;
using KotoDibo.Domain.Enums;

namespace KotoDibo.UnitTests.Domain.Calculations;

public class BudgetCalculatorTests
{
    private static CategoryBudgetInput Category(decimal planned, decimal spent, decimal rollover = 0m) => new()
    {
        CategoryAllocationId = "alloc-1",
        CategoryId = "cat-1",
        CategoryName = "Food",
        PlannedAmount = planned,
        RolloverAmount = rollover,
        Spent = spent,
    };

    [Fact]
    public void EvaluateCategory_healthy_when_under_warning_threshold()
    {
        var result = BudgetCalculator.EvaluateCategory(Category(20000m, 16500m));

        result.Remaining.Should().Be(3500m);
        result.Variance.Should().Be(3500m);
        result.UsagePercentage.Should().Be(82.5m);
        result.Status.Should().Be(BudgetCategoryStatus.Warning);
    }

    [Fact]
    public void EvaluateCategory_ontrack_below_warning_threshold()
    {
        var result = BudgetCalculator.EvaluateCategory(Category(20000m, 10000m));

        result.UsagePercentage.Should().Be(50m);
        result.Status.Should().Be(BudgetCategoryStatus.OnTrack);
    }

    [Fact]
    public void EvaluateCategory_overspent_when_spend_exceeds_available()
    {
        var result = BudgetCalculator.EvaluateCategory(Category(20000m, 23000m));

        result.Remaining.Should().Be(-3000m);
        result.Variance.Should().Be(-3000m);
        result.UsagePercentage.Should().Be(115m);
        result.Status.Should().Be(BudgetCategoryStatus.Overspent);
    }

    [Fact]
    public void EvaluateCategory_rollover_adds_to_total_available()
    {
        var result = BudgetCalculator.EvaluateCategory(Category(20000m, 12000m, rollover: 5000m));

        result.TotalAvailable.Should().Be(25000m);
        result.Remaining.Should().Be(13000m);
        result.Status.Should().Be(BudgetCategoryStatus.OnTrack);
    }

    [Fact]
    public void EvaluateCategory_no_budget_when_nothing_allocated()
    {
        var result = BudgetCalculator.EvaluateCategory(Category(0m, 0m));

        result.UsagePercentage.Should().BeNull();
        result.Status.Should().Be(BudgetCategoryStatus.NoBudget);
    }

    [Fact]
    public void EvaluateCategory_no_budget_does_not_divide_by_zero_even_with_spend()
    {
        var act = () => BudgetCalculator.EvaluateCategory(Category(0m, 500m));

        act.Should().NotThrow();
        var result = BudgetCalculator.EvaluateCategory(Category(0m, 500m));
        result.UsagePercentage.Should().BeNull();
        result.Status.Should().Be(BudgetCategoryStatus.NoBudget);
        result.Remaining.Should().Be(-500m);
    }

    [Fact]
    public void Summarize_aggregates_totals_and_uncategorized_spend()
    {
        var categories = new List<CategoryBudgetInput>
        {
            Category(20000m, 16500m) with { CategoryAllocationId = "food", CategoryName = "Food" },
            Category(5000m, 7500m) with { CategoryAllocationId = "fun", CategoryName = "Entertainment" },
        };

        var summary = BudgetCalculator.Summarize(categories, uncategorizedSpent: 1000m);

        summary.TotalPlanned.Should().Be(25000m);
        summary.TotalSpent.Should().Be(25000m); // 16500 + 7500 + 1000
        summary.TotalRemaining.Should().Be(0m);
        summary.TotalOverspent.Should().Be(2500m); // Entertainment's 2500 overspend
        summary.Categories.Should().HaveCount(2);
    }

    [Fact]
    public void Summarize_no_categories_returns_zeroed_no_budget_summary()
    {
        var summary = BudgetCalculator.Summarize([], uncategorizedSpent: 0m);

        summary.TotalAvailable.Should().Be(0m);
        summary.UtilizationPercentage.Should().BeNull();
        summary.Health.Should().Be(BudgetHealthStatus.NoBudget);
        summary.Categories.Should().BeEmpty();
    }

    [Fact]
    public void Summarize_health_escalates_to_critical_purely_from_overspent_category_count()
    {
        // Overall utilization stays well under the Critical percentage bar (13.3%), but 3
        // individually-overspent categories should still escalate health on their own.
        var categories = new List<CategoryBudgetInput>
        {
            Category(1000m, 1100m) with { CategoryAllocationId = "a" },
            Category(1000m, 1100m) with { CategoryAllocationId = "b" },
            Category(1000m, 1100m) with { CategoryAllocationId = "c" },
            Category(100000m, 10000m) with { CategoryAllocationId = "d", CategoryName = "Housing" },
        };

        var summary = BudgetCalculator.Summarize(categories, uncategorizedSpent: 0m);

        summary.UtilizationPercentage.Should().BeLessThan(BudgetThresholds.OverallCriticalPercentage);
        summary.Health.Should().Be(BudgetHealthStatus.Critical);
    }

    [Fact]
    public void Summarize_health_is_healthy_when_well_under_budget()
    {
        var categories = new List<CategoryBudgetInput> { Category(10000m, 1000m) };

        var summary = BudgetCalculator.Summarize(categories, uncategorizedSpent: 0m);

        summary.Health.Should().Be(BudgetHealthStatus.Healthy);
    }
}
