using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BudgetDashboard.DTOs;
using KotoDibo.Application.Features.BudgetDashboard.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using BudgetEntity = KotoDibo.Domain.Entities.Budget;
using Moq;

namespace KotoDibo.UnitTests.Features.BudgetDashboard;

public class BudgetDashboardServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<Expense>> _expenses = new();
    private readonly Mock<IRepository<BudgetEntity>> _budgets = new();
    private readonly Mock<IRepository<BudgetCategoryAllocation>> _allocations = new();
    private readonly Mock<IRepository<RecurringExpense>> _recurringExpenses = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly BudgetDashboardService _sut;

    public BudgetDashboardServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _budgets.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BudgetEntity, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _allocations.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BudgetCategoryAllocation, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _recurringExpenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RecurringExpense, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _sut = new BudgetDashboardService(_expenses.Object, _budgets.Object, _allocations.Object, _recurringExpenses.Object, _dateTimeProvider.Object);
    }

    [Fact]
    public async Task GetDashboardAsync_defaults_to_this_month_when_no_period_given()
    {
        var result = await _sut.GetDashboardAsync("user-1", new DashboardQuery(), CancellationToken.None);

        result.Period.From.Should().Be(new DateOnly(2026, 1, 1));
        result.Period.To.Should().Be(new DateOnly(2026, 1, 31));
    }

    [Fact]
    public async Task GetDashboardAsync_no_budget_no_expenses_returns_zeroed_response()
    {
        var result = await _sut.GetDashboardAsync("user-1", new DashboardQuery { ComparisonPeriod = DashboardComparisonPeriod.None }, CancellationToken.None);

        result.Budget.HasBudget.Should().BeFalse();
        result.Summary.TotalSpent.Should().Be(0m);
        result.Summary.BudgetUtilizationPercentage.Should().BeNull();
        result.CategoryBreakdown.Should().BeEmpty();
        result.Overspending.Should().BeEmpty();
        result.Comparison.Should().BeNull();
    }

    [Fact]
    public async Task GetDashboardAsync_matches_active_budget_and_flags_overspending_category()
    {
        var budget = new BudgetEntity
        {
            Id = "budget-1", UserId = "user-1", Name = "January",
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active,
        };
        _budgets.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BudgetEntity, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync([budget]);
        _allocations.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BudgetCategoryAllocation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BudgetCategoryAllocation { Id = "alloc-1", BudgetId = "budget-1", CategoryId = "cat-fun", CategoryName = "Entertainment", PlannedAmount = 5000m }]);
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Expense { UserId = "user-1", CategoryId = "cat-fun", CategoryName = "Entertainment", Amount = 7500m, Date = new DateOnly(2026, 1, 15), Merchant = "Cineplex" }]);

        var result = await _sut.GetDashboardAsync("user-1", new DashboardQuery { ComparisonPeriod = DashboardComparisonPeriod.None }, CancellationToken.None);

        result.Budget.HasBudget.Should().BeTrue();
        result.Budget.Id.Should().Be("budget-1");
        result.Summary.TotalSpent.Should().Be(7500m);
        result.Summary.TotalRemaining.Should().Be(-2500m);
        result.Overspending.Should().ContainSingle(c => c.CategoryName == "Entertainment");
        result.TopCategories.Should().ContainSingle(c => c.CategoryName == "Entertainment" && c.PercentageOfTotal == 100m);
        result.TopMerchants.Should().ContainSingle(m => m.Merchant == "Cineplex");
        result.Insights.OverspendingCategoriesCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardAsync_computes_previous_period_comparison()
    {
        var januaryExpense = new Expense { UserId = "user-1", CategoryId = "c", CategoryName = "Food", Amount = 1000m, Date = new DateOnly(2026, 1, 15) };
        var decemberExpense = new Expense { UserId = "user-1", CategoryId = "c", CategoryName = "Food", Amount = 500m, Date = new DateOnly(2025, 12, 15) };

        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Expense, bool>> predicate, CancellationToken _) =>
                new List<Expense> { januaryExpense, decemberExpense }.Where(predicate.Compile()).ToList());

        var query = new DashboardQuery
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 1, 31),
            ComparisonPeriod = DashboardComparisonPeriod.PreviousPeriod,
        };

        var result = await _sut.GetDashboardAsync("user-1", query, CancellationToken.None);

        result.Comparison.Should().NotBeNull();
        result.Comparison!.CurrentSpending.Should().Be(1000m);
        result.Comparison.PreviousSpending.Should().Be(500m);
        result.Comparison.SpendingChange.Should().Be(500m);
        result.Comparison.SpendingChangePercentage.Should().Be(100m);
        result.Comparison.Trend.Should().Be("Increased");
    }

    [Fact]
    public async Task GetDashboardAsync_lists_upcoming_recurring_expenses_within_horizon()
    {
        _recurringExpenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RecurringExpense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RecurringExpense { Id = "r1", UserId = "user-1", CategoryName = "Subscriptions", Merchant = "Netflix", Amount = 1200m, IsActive = true, NextOccurrenceDate = new DateOnly(2026, 1, 23) },
            ]);

        var result = await _sut.GetDashboardAsync("user-1", new DashboardQuery { ComparisonPeriod = DashboardComparisonPeriod.None }, CancellationToken.None);

        result.UpcomingExpenses.Should().ContainSingle(u => u.Merchant == "Netflix" && u.DaysUntilDue == 3);
    }
}
