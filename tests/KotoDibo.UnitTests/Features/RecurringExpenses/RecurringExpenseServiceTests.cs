using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Application.Features.RecurringExpenses.DTOs;
using KotoDibo.Application.Features.RecurringExpenses.Services;
using KotoDibo.Application.Features.RecurringExpenses.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.RecurringExpenses;

public class RecurringExpenseServiceTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<RecurringExpense>> _recurring = new();
    private readonly Mock<IRepository<Expense>> _expenses = new();
    private readonly Mock<IExpenseCategoryService> _categories = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly RecurringExpenseService _sut;

    public RecurringExpenseServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _recurring.Setup(x => x.AddAsync(It.IsAny<RecurringExpense>(), It.IsAny<CancellationToken>()))
            .Callback<RecurringExpense, CancellationToken>((r, _) => r.Id = "recurring-1")
            .ReturnsAsync((RecurringExpense r, CancellationToken _) => r);
        _expenses.Setup(x => x.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()))
            .Callback<Expense, CancellationToken>((e, _) => e.Id = Guid.NewGuid().ToString())
            .ReturnsAsync((Expense e, CancellationToken _) => e);
        _categories.Setup(x => x.RequireVisibleAsync(It.IsAny<string>(), "cat-sub", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "cat-sub", Name = "Subscriptions", IsSystemDefault = true });

        _sut = new RecurringExpenseService(
            _recurring.Object,
            _expenses.Object,
            _categories.Object,
            _dateTimeProvider.Object,
            new CreateRecurringExpenseRequestValidator(),
            new UpdateRecurringExpenseRequestValidator());
    }

    private static CreateRecurringExpenseRequest ValidRequest() => new()
    {
        Amount = 1200m,
        CategoryId = "cat-sub",
        Merchant = "Netflix",
        Frequency = nameof(RecurrenceFrequency.Monthly),
        StartDate = new DateOnly(2026, 1, 1),
    };

    [Fact]
    public async Task CreateAsync_initializes_next_occurrence_to_start_date()
    {
        var result = await _sut.CreateAsync("user-1", ValidRequest(), CancellationToken.None);

        result.NextOccurrenceDate.Should().Be(new DateOnly(2026, 1, 1));
        result.LastGeneratedDate.Should().BeNull();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_rejects_end_date_before_start_date()
    {
        var request = ValidRequest() with { EndDate = new DateOnly(2025, 12, 1) };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_frequency()
    {
        var request = ValidRequest() with { Frequency = "Fortnightly" };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task GenerateDueOccurrencesAsync_materializes_missed_occurrences_and_advances_state()
    {
        var recurring = new RecurringExpense
        {
            Id = "recurring-1",
            UserId = "user-1",
            Amount = 1200m,
            Currency = "BDT",
            CategoryId = "cat-sub",
            CategoryName = "Subscriptions",
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            NextOccurrenceDate = new DateOnly(2026, 1, 1),
            IsActive = true,
        };
        _recurring.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RecurringExpense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([recurring]);

        var generated = await _sut.GenerateDueOccurrencesAsync("user-1", new DateOnly(2026, 4, 1), CancellationToken.None);

        generated.Should().HaveCount(4); // Jan, Feb, Mar, Apr
        generated.Should().OnlyContain(e => e.RecurringExpenseId == "recurring-1" && e.IsRecurringGenerated);
        recurring.LastGeneratedDate.Should().Be(new DateOnly(2026, 4, 1));
        recurring.NextOccurrenceDate.Should().Be(new DateOnly(2026, 5, 1));
        _recurring.Verify(x => x.UpdateAsync(recurring, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateDueOccurrencesAsync_is_a_noop_when_nothing_is_due()
    {
        var recurring = new RecurringExpense
        {
            Id = "recurring-1",
            UserId = "user-1",
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            NextOccurrenceDate = new DateOnly(2026, 1, 1),
            LastGeneratedDate = new DateOnly(2026, 4, 1),
            IsActive = true,
        };
        _recurring.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RecurringExpense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([recurring]);

        var generated = await _sut.GenerateDueOccurrencesAsync("user-1", new DateOnly(2026, 4, 1), CancellationToken.None);

        generated.Should().BeEmpty();
        _recurring.Verify(x => x.UpdateAsync(It.IsAny<RecurringExpense>(), It.IsAny<CancellationToken>()), Times.Never);
        _expenses.Verify(x => x.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateDueOccurrencesAsync_treats_a_duplicate_key_conflict_as_already_generated()
    {
        var recurring = new RecurringExpense
        {
            Id = "recurring-1",
            UserId = "user-1",
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            NextOccurrenceDate = new DateOnly(2026, 1, 1),
            IsActive = true,
        };
        _recurring.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RecurringExpense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([recurring]);
        _expenses.Setup(x => x.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateKeyException("already exists"));

        var generated = await _sut.GenerateDueOccurrencesAsync("user-1", new DateOnly(2026, 1, 1), CancellationToken.None);

        generated.Should().BeEmpty("the insert conflicted, meaning another run already created it");
        recurring.LastGeneratedDate.Should().Be(new DateOnly(2026, 1, 1), "state still advances even though this run didn't insert it itself");
    }

    [Fact]
    public async Task UpdateAsync_throws_when_recurring_expense_belongs_to_another_user()
    {
        _recurring.Setup(x => x.GetByIdAsync("recurring-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecurringExpense { Id = "recurring-1", UserId = "user-2" });

        var act = () => _sut.UpdateAsync("user-1", "recurring-1", new UpdateRecurringExpenseRequest { Amount = 10m }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
