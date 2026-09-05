using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Services;
using KotoDibo.Application.Features.Budget.Validators;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using BudgetEntity = KotoDibo.Domain.Entities.Budget;
using Moq;

namespace KotoDibo.UnitTests.Features.Budget;

public class BudgetServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<BudgetEntity>> _budgets = new();
    private readonly Mock<IRepository<BudgetCategoryAllocation>> _allocations = new();
    private readonly Mock<IRepository<BudgetAdjustment>> _adjustments = new();
    private readonly Mock<IRepository<Expense>> _expenses = new();
    private readonly Mock<IExpenseCategoryService> _categories = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly BudgetService _sut;
    private readonly List<BudgetCategoryAllocation> _storedAllocations = [];

    public BudgetServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);

        // Starts well past any hardcoded "budget-1" literal the tests set up by hand, so a
        // service-driven AddAsync (e.g. RolloverAsync creating the next period) never collides
        // with a pre-seeded fixture budget's id.
        var nextBudgetId = 100;
        _budgets.Setup(x => x.AddAsync(It.IsAny<BudgetEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BudgetEntity, CancellationToken>((b, _) => b.Id = $"budget-{nextBudgetId++}")
            .ReturnsAsync((BudgetEntity b, CancellationToken _) => b);

        _allocations.Setup(x => x.AddAsync(It.IsAny<BudgetCategoryAllocation>(), It.IsAny<CancellationToken>()))
            .Callback<BudgetCategoryAllocation, CancellationToken>((a, _) =>
            {
                a.Id = $"alloc-{_storedAllocations.Count + 1}";
                _storedAllocations.Add(a);
            })
            .ReturnsAsync((BudgetCategoryAllocation a, CancellationToken _) => a);
        _allocations.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BudgetCategoryAllocation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<BudgetCategoryAllocation, bool>> predicate, CancellationToken _) =>
                _storedAllocations.Where(predicate.Compile()).ToList());
        _allocations.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<BudgetCategoryAllocation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<BudgetCategoryAllocation, bool>> predicate, CancellationToken _) =>
                _storedAllocations.FirstOrDefault(predicate.Compile()));
        _allocations.Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _storedAllocations.FirstOrDefault(a => a.Id == id));

        _categories.Setup(x => x.RequireVisibleAsync(It.IsAny<string>(), "cat-food", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "cat-food", Name = "Food", IsSystemDefault = true });
        _categories.Setup(x => x.RequireVisibleAsync(It.IsAny<string>(), "cat-fun", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "cat-fun", Name = "Entertainment", IsSystemDefault = true });

        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new BudgetService(
            _budgets.Object,
            _allocations.Object,
            _adjustments.Object,
            _expenses.Object,
            _categories.Object,
            _dateTimeProvider.Object,
            new CreateBudgetRequestValidator(),
            new UpdateBudgetRequestValidator(),
            new AddBudgetCategoryRequestValidator(),
            new AdjustBudgetCategoryRequestValidator(),
            new TransferBudgetCategoryRequestValidator(),
            new RolloverBudgetRequestValidator());
    }

    private static CreateBudgetRequest ValidRequest() => new()
    {
        Name = "January 2026",
        PeriodType = nameof(BudgetPeriodType.Monthly),
        StartDate = new DateOnly(2026, 1, 1),
    };

    [Fact]
    public async Task CreateAsync_persists_budget_scoped_to_caller_and_derives_month_end()
    {
        var result = await _sut.CreateAsync("user-1", ValidRequest(), CancellationToken.None);

        result.Name.Should().Be("January 2026");
        result.EndDate.Should().Be(new DateOnly(2026, 1, 31));
        result.Currency.Should().Be("BDT");
        result.Status.Should().Be(nameof(BudgetStatus.Draft));
        _budgets.Verify(x => x.AddAsync(It.Is<BudgetEntity>(b => b.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_requires_explicit_end_date_for_custom_period()
    {
        var request = ValidRequest() with { PeriodType = nameof(BudgetPeriodType.Custom), EndDate = null };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_creates_initial_category_allocations()
    {
        var request = ValidRequest() with
        {
            Categories = [new CreateBudgetCategoryInput { CategoryId = "cat-food", PlannedAmount = 20000m, RolloverEnabled = true }],
        };

        var result = await _sut.CreateAsync("user-1", request, CancellationToken.None);

        result.Categories.Should().ContainSingle(c => c.CategoryName == "Food" && c.PlannedAmount == 20000m && c.RolloverEnabled);
        _adjustments.Verify(x => x.AddAsync(It.Is<BudgetAdjustment>(a => a.Type == BudgetAdjustmentType.Initial), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_duplicateCategoryId_ThrowsValidationExceptionBeforeAnyWrite()
    {
        var request = ValidRequest() with
        {
            Categories =
            [
                new CreateBudgetCategoryInput { CategoryId = "cat-food", PlannedAmount = 20000m },
                new CreateBudgetCategoryInput { CategoryId = "cat-food", PlannedAmount = 5000m },
            ],
        };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _budgets.Verify(x => x.AddAsync(It.IsAny<BudgetEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_budget_belongs_to_another_user()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31) };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var act = () => _sut.GetByIdAsync("user-2", "budget-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_computes_overspent_category_from_live_expenses()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "January", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-1", BudgetId = "budget-1", CategoryId = "cat-fun", CategoryName = "Entertainment", PlannedAmount = 5000m });
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Expense { UserId = "user-1", CategoryId = "cat-fun", Amount = 7500m, Date = new DateOnly(2026, 1, 15), Status = FinancialEntryStatus.Active }]);

        var result = await _sut.GetByIdAsync("user-1", "budget-1", CancellationToken.None);

        var category = result.Categories.Single();
        category.Spent.Should().Be(7500m);
        category.Remaining.Should().Be(-2500m);
        category.Status.Should().Be(nameof(BudgetCategoryStatus.Overspent));
        result.Health.Should().Be(nameof(BudgetHealthStatus.Critical)); // 150% overall utilization crosses the Critical bar
    }

    [Theory]
    [InlineData(nameof(BudgetStatus.Draft), nameof(BudgetStatus.Active), true)]
    [InlineData(nameof(BudgetStatus.Draft), nameof(BudgetStatus.Completed), false)]
    [InlineData(nameof(BudgetStatus.Active), nameof(BudgetStatus.Completed), true)]
    [InlineData(nameof(BudgetStatus.Completed), nameof(BudgetStatus.Active), false)]
    [InlineData(nameof(BudgetStatus.Archived), nameof(BudgetStatus.Active), false)]
    public async Task UpdateAsync_enforces_status_transition_rules(string from, string to, bool allowed)
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = Enum.Parse<BudgetStatus>(from) };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var act = () => _sut.UpdateAsync("user-1", "budget-1", new UpdateBudgetRequest { Status = to }, CancellationToken.None);

        if (allowed)
        {
            await act.Should().NotThrowAsync();
        }
        else
        {
            await act.Should().ThrowAsync<ValidationException>();
        }
    }

    [Fact]
    public async Task AddCategoryAsync_rejects_duplicate_category_for_same_budget()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-1", BudgetId = "budget-1", CategoryId = "cat-food", CategoryName = "Food", PlannedAmount = 1000m });

        var act = () => _sut.AddCategoryAsync("user-1", "budget-1", new AddBudgetCategoryRequest { CategoryId = "cat-food", PlannedAmount = 500m }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AddCategoryAsync_rejects_edits_on_archived_budget()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Archived };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var act = () => _sut.AddCategoryAsync("user-1", "budget-1", new AddBudgetCategoryRequest { CategoryId = "cat-food", PlannedAmount = 500m }, CancellationToken.None);

        await act.Should().ThrowAsync<KotoDibo.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public async Task AdjustCategoryAsync_increases_planned_amount_and_records_history()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-1", BudgetId = "budget-1", CategoryId = "cat-food", CategoryName = "Food", PlannedAmount = 1000m });

        var result = await _sut.AdjustCategoryAsync("user-1", "budget-1", "alloc-1", new AdjustBudgetCategoryRequest { Delta = 500m }, CancellationToken.None);

        result.Categories.Single().PlannedAmount.Should().Be(1500m);
        _adjustments.Verify(x => x.AddAsync(It.Is<BudgetAdjustment>(a => a.Type == BudgetAdjustmentType.Increase && a.Amount == 500m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustCategoryAsync_rejects_decrease_below_zero()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-1", BudgetId = "budget-1", CategoryId = "cat-food", CategoryName = "Food", PlannedAmount = 1000m });

        var act = () => _sut.AdjustCategoryAsync("user-1", "budget-1", "alloc-1", new AdjustBudgetCategoryRequest { Delta = -1500m }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task TransferCategoryAsync_moves_amount_between_categories()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-food", BudgetId = "budget-1", CategoryId = "cat-food", CategoryName = "Food", PlannedAmount = 5000m });
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-fun", BudgetId = "budget-1", CategoryId = "cat-fun", CategoryName = "Entertainment", PlannedAmount = 1000m });

        var result = await _sut.TransferCategoryAsync("user-1", "budget-1", "alloc-food", new TransferBudgetCategoryRequest { ToCategoryAllocationId = "alloc-fun", Amount = 2000m }, CancellationToken.None);

        result.Categories.First(c => c.CategoryId == "cat-food").PlannedAmount.Should().Be(3000m);
        result.Categories.First(c => c.CategoryId == "cat-fun").PlannedAmount.Should().Be(3000m);
        _adjustments.Verify(x => x.AddAsync(It.Is<BudgetAdjustment>(a => a.Type == BudgetAdjustmentType.TransferOut), It.IsAny<CancellationToken>()), Times.Once);
        _adjustments.Verify(x => x.AddAsync(It.Is<BudgetAdjustment>(a => a.Type == BudgetAdjustmentType.TransferIn), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferCategoryAsync_rejects_amount_exceeding_source_planned_amount()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "X", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-food", BudgetId = "budget-1", CategoryId = "cat-food", CategoryName = "Food", PlannedAmount = 100m });
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-fun", BudgetId = "budget-1", CategoryId = "cat-fun", CategoryName = "Entertainment", PlannedAmount = 1000m });

        var act = () => _sut.TransferCategoryAsync("user-1", "budget-1", "alloc-food", new TransferBudgetCategoryRequest { ToCategoryAllocationId = "alloc-fun", Amount = 2000m }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RolloverAsync_carries_forward_remaining_for_rollover_enabled_categories_only()
    {
        var current = new BudgetEntity { Id = "budget-1", UserId = "user-1", Name = "January", PeriodType = BudgetPeriodType.Monthly, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), Status = BudgetStatus.Active };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(current);
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-food", BudgetId = "budget-1", CategoryId = "cat-food", CategoryName = "Food", PlannedAmount = 20000m, RolloverEnabled = true });
        _storedAllocations.Add(new BudgetCategoryAllocation { Id = "alloc-fun", BudgetId = "budget-1", CategoryId = "cat-fun", CategoryName = "Entertainment", PlannedAmount = 5000m, RolloverEnabled = false });
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Expense { UserId = "user-1", CategoryId = "cat-food", Amount = 15000m, Date = new DateOnly(2026, 1, 10), Status = FinancialEntryStatus.Active }]);

        var next = await _sut.RolloverAsync("user-1", "budget-1", new RolloverBudgetRequest(), CancellationToken.None);

        next.StartDate.Should().Be(new DateOnly(2026, 2, 1));
        next.EndDate.Should().Be(new DateOnly(2026, 2, 28));
        next.Status.Should().Be(nameof(BudgetStatus.Draft));

        var food = next.Categories.Single(c => c.CategoryId == "cat-food");
        food.PlannedAmount.Should().Be(20000m);
        food.RolloverAmount.Should().Be(5000m); // 20000 planned - 15000 spent

        var fun = next.Categories.Single(c => c.CategoryId == "cat-fun");
        fun.RolloverAmount.Should().Be(0m); // rollover disabled for this category
    }
}
