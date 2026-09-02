using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.Expenses.Services;
using KotoDibo.Application.Features.Expenses.Validators;
using KotoDibo.Domain.Entities;
using Moq;

namespace KotoDibo.UnitTests.Features.Expenses;

public class ExpenseServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<Expense>> _expenses = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly ExpenseService _sut;

    public ExpenseServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _expenses.Setup(x => x.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()))
            .Callback<Expense, CancellationToken>((e, _) => e.Id = "expense-1")
            .ReturnsAsync((Expense e, CancellationToken _) => e);

        _sut = new ExpenseService(_expenses.Object, _dateTimeProvider.Object, new CreateExpenseRequestValidator());
    }

    private static CreateExpenseRequest ValidRequest() => new()
    {
        Amount = 250m,
        Category = "Groceries",
        Description = "Weekly shopping",
        Date = Today,
    };

    [Fact]
    public async Task CreateAsync_persists_expense_scoped_to_caller()
    {
        var result = await _sut.CreateAsync("user-1", ValidRequest(), CancellationToken.None);

        result.Amount.Should().Be(250m);
        result.Category.Should().Be("Groceries");
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
    public async Task GetByIdAsync_throws_when_expense_belongs_to_another_user()
    {
        var expense = new Expense { Id = "expense-1", UserId = "user-1", Amount = 100m, Category = "Food", Date = Today };
        _expenses.Setup(x => x.GetByIdAsync("expense-1", It.IsAny<CancellationToken>())).ReturnsAsync(expense);

        var act = () => _sut.GetByIdAsync("user-2", "expense-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAllAsync_filters_by_date_range()
    {
        var inRange = new Expense { Id = "e1", UserId = "user-1", Amount = 100m, Category = "Food", Date = Today };
        _expenses.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Expense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([inRange]);

        var result = await _sut.GetAllAsync("user-1", Today, Today, CancellationToken.None);

        result.Should().ContainSingle(e => e.Id == "e1");
    }
}
