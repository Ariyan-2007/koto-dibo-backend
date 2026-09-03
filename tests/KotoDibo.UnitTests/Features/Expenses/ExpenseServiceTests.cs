using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.Expenses.Services;
using KotoDibo.Application.Features.Expenses.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.Expenses;

public class ExpenseServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<Expense>> _expenses = new();
    private readonly Mock<IExpenseCategoryService> _categories = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly ExpenseService _sut;

    public ExpenseServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _expenses.Setup(x => x.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()))
            .Callback<Expense, CancellationToken>((e, _) => e.Id = "expense-1")
            .ReturnsAsync((Expense e, CancellationToken _) => e);
        _categories.Setup(x => x.RequireVisibleAsync("user-1", "cat-groceries", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "cat-groceries", Name = "Groceries", IsSystemDefault = true });

        _sut = new ExpenseService(
            _expenses.Object,
            _categories.Object,
            _dateTimeProvider.Object,
            new CreateExpenseRequestValidator(),
            new UpdateExpenseRequestValidator());
    }

    private static CreateExpenseRequest ValidRequest() => new()
    {
        Amount = 250m,
        CategoryId = "cat-groceries",
        Description = "Weekly shopping",
        Date = Today,
    };

    [Fact]
    public async Task CreateAsync_persists_expense_scoped_to_caller()
    {
        var result = await _sut.CreateAsync("user-1", ValidRequest(), CancellationToken.None);

        result.Amount.Should().Be(250m);
        result.CategoryName.Should().Be("Groceries");
        result.Currency.Should().Be("BDT");
        result.PaymentMethod.Should().Be(nameof(ExpensePaymentMethod.Cash));
        _expenses.Verify(x => x.AddAsync(It.Is<Expense>(e => e.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_rejects_zero_amount()
    {
        var request = ValidRequest() with { Amount = 0 };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_future_dated_expense()
    {
        var request = ValidRequest() with { Date = Today.AddDays(1) };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_category()
    {
        _categories.Setup(x => x.RequireVisibleAsync("user-1", "cat-unknown", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("ExpenseCategory", "cat-unknown"));

        var request = ValidRequest() with { CategoryId = "cat-unknown" };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_normalizes_and_deduplicates_tags()
    {
        var request = ValidRequest() with { Tags = ["Vacation", " vacation ", "Work"] };

        var result = await _sut.CreateAsync("user-1", request, CancellationToken.None);

        result.Tags.Should().BeEquivalentTo(["Vacation", "Work"]);
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_expense_belongs_to_another_user()
    {
        var expense = new Expense { Id = "expense-1", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today };
        _expenses.Setup(x => x.GetByIdAsync("expense-1", It.IsAny<CancellationToken>())).ReturnsAsync(expense);

        var act = () => _sut.GetByIdAsync("user-2", "expense-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_date_range_and_paginates()
    {
        var inRange = new Expense { Id = "e1", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today, Status = FinancialEntryStatus.Active };
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([inRange]);

        var result = await _sut.GetPagedAsync("user-1", new ExpenseListQuery { From = Today, To = Today }, CancellationToken.None);

        result.Items.Should().ContainSingle(e => e.Id == "e1");
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_tag_in_memory()
    {
        var tagged = new Expense { Id = "e1", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today, Tags = ["work"] };
        var untagged = new Expense { Id = "e2", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today, Tags = [] };
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([tagged, untagged]);

        var result = await _sut.GetPagedAsync("user-1", new ExpenseListQuery { Tag = "work" }, CancellationToken.None);

        result.Items.Should().ContainSingle(e => e.Id == "e1");
    }

    [Fact]
    public async Task GetPagedAsync_sorts_by_amount_descending()
    {
        var small = new Expense { Id = "small", UserId = "user-1", Amount = 50m, CategoryId = "c", CategoryName = "Food", Date = Today };
        var large = new Expense { Id = "large", UserId = "user-1", Amount = 500m, CategoryId = "c", CategoryName = "Food", Date = Today };
        _expenses.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([small, large]);

        var result = await _sut.GetPagedAsync("user-1", new ExpenseListQuery { SortBy = ExpenseSortField.Amount, SortDescending = true }, CancellationToken.None);

        result.Items.Select(e => e.Id).Should().Equal("large", "small");
    }

    [Fact]
    public async Task UpdateAsync_applies_only_provided_fields()
    {
        var expense = new Expense { Id = "expense-1", UserId = "user-1", Amount = 100m, CategoryId = "cat-groceries", CategoryName = "Groceries", Description = "Old", Date = Today, Status = FinancialEntryStatus.Active };
        _expenses.Setup(x => x.GetByIdAsync("expense-1", It.IsAny<CancellationToken>())).ReturnsAsync(expense);

        var result = await _sut.UpdateAsync("user-1", "expense-1", new UpdateExpenseRequest { Amount = 300m }, CancellationToken.None);

        result.Amount.Should().Be(300m);
        result.Description.Should().Be("Old");
    }

    [Fact]
    public async Task UpdateAsync_rejects_editing_a_deleted_expense()
    {
        var expense = new Expense { Id = "expense-1", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today, Status = FinancialEntryStatus.Cancelled };
        _expenses.Setup(x => x.GetByIdAsync("expense-1", It.IsAny<CancellationToken>())).ReturnsAsync(expense);

        var act = () => _sut.UpdateAsync("user-1", "expense-1", new UpdateExpenseRequest { Amount = 10m }, CancellationToken.None);

        await act.Should().ThrowAsync<KotoDibo.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_by_setting_cancelled_status()
    {
        var expense = new Expense { Id = "expense-1", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today, Status = FinancialEntryStatus.Active };
        _expenses.Setup(x => x.GetByIdAsync("expense-1", It.IsAny<CancellationToken>())).ReturnsAsync(expense);

        var result = await _sut.DeleteAsync("user-1", "expense-1", CancellationToken.None);

        result.Status.Should().Be(nameof(FinancialEntryStatus.Cancelled));
        _expenses.Verify(x => x.UpdateAsync(It.Is<Expense>(e => e.Status == FinancialEntryStatus.Cancelled), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_expense_belongs_to_another_user()
    {
        var expense = new Expense { Id = "expense-1", UserId = "user-1", Amount = 100m, CategoryId = "c", CategoryName = "Food", Date = Today };
        _expenses.Setup(x => x.GetByIdAsync("expense-1", It.IsAny<CancellationToken>())).ReturnsAsync(expense);

        var act = () => _sut.DeleteAsync("user-2", "expense-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
