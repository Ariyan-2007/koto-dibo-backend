using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Services;
using KotoDibo.Application.Features.Budget.Validators;
using BudgetEntity = KotoDibo.Domain.Entities.Budget;
using Moq;

namespace KotoDibo.UnitTests.Features.Budget;

public class BudgetServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<BudgetEntity>> _budgets = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly BudgetService _sut;

    public BudgetServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _budgets.Setup(x => x.AddAsync(It.IsAny<BudgetEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BudgetEntity, CancellationToken>((b, _) => b.Id = "budget-1")
            .ReturnsAsync((BudgetEntity b, CancellationToken _) => b);

        _sut = new BudgetService(_budgets.Object, _dateTimeProvider.Object, new CreateBudgetRequestValidator());
    }

    private static CreateBudgetRequest ValidRequest() => new() { Period = "2026-01", Amount = 5000m };

    [Fact]
    public async Task CreateAsync_persists_budget_scoped_to_caller()
    {
        var result = await _sut.CreateAsync("user-1", ValidRequest(), CancellationToken.None);

        result.Period.Should().Be("2026-01");
        result.Amount.Should().Be(5000m);
        _budgets.Verify(x => x.AddAsync(It.Is<BudgetEntity>(b => b.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_rejects_malformed_period()
    {
        var request = ValidRequest() with { Period = "January 2026" };

        var act = () => _sut.CreateAsync("user-1", request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_period_for_same_user()
    {
        _budgets.Setup(x => x.FindOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<BudgetEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BudgetEntity { Id = "existing", UserId = "user-1", Period = "2026-01", Amount = 1000m });

        var act = () => _sut.CreateAsync("user-1", ValidRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_budget_belongs_to_another_user()
    {
        var budget = new BudgetEntity { Id = "budget-1", UserId = "user-1", Period = "2026-01", Amount = 5000m };
        _budgets.Setup(x => x.GetByIdAsync("budget-1", It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var act = () => _sut.GetByIdAsync("user-2", "budget-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
