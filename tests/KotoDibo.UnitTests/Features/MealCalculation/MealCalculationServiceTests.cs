using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Application.Features.MealCalculation.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.MealCalculation;

public class MealCalculationServiceTests
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 1, 31);

    private readonly Mock<IRepository<BazarPurchase>> _purchases = new();
    private readonly Mock<IRepository<Contribution>> _contributions = new();
    private readonly Mock<IRepository<DailyMealEntry>> _mealEntries = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();

    private readonly MealCalculationService _sut;

    public MealCalculationServiceTests()
    {
        var access = new HouseholdAccessService(_memberships.Object);
        _sut = new MealCalculationService(_purchases.Object, _contributions.Object, _mealEntries.Object, access);

        _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMembership
            {
                Id = "membership-1",
                HouseholdId = "household-1",
                UserId = "caller-1",
                Role = HouseholdRole.Member,
                Status = HouseholdMembershipStatus.Active,
            });

        _contributions.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Contribution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private static BazarPurchase Purchase(string buyer, decimal amount) => new()
    {
        Id = Guid.NewGuid().ToString(),
        HouseholdId = "household-1",
        PurchasedByUserId = buyer,
        Date = From,
        Amount = amount,
        Currency = "BDT",
        Status = FinancialEntryStatus.Active,
    };

    private static Contribution ContributionEntry(string userId, decimal amount) => new()
    {
        Id = Guid.NewGuid().ToString(),
        HouseholdId = "household-1",
        ContributedByUserId = userId,
        Date = From,
        Amount = amount,
        Currency = "BDT",
        Status = FinancialEntryStatus.Active,
    };

    private static DailyMealEntry MealEntry(string userId, decimal count, DateOnly date) => new()
    {
        Id = Guid.NewGuid().ToString(),
        HouseholdId = "household-1",
        UserId = userId,
        Date = date,
        Count = count,
        Status = DailyMealEntryStatus.Active,
    };

    // Real numbers from House No. 289's January 2026 sheet (traced via cell formulas):
    // Total shopping spend = 17000, total meal units = 292 across 8 members.
    [Fact]
    public async Task GetMealRateAsync_JanuaryWorkedExample_MatchesSpreadsheetTotals()
    {
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Purchase("ariyan", 17000m)]);

        _mealEntries.Setup(x => x.FindAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                MealEntry("ariyan", 53, From),
                MealEntry("waythin", 45, From),
                MealEntry("raju", 18, From),
                MealEntry("rabiul", 21, From),
                MealEntry("ayon", 30, From),
                MealEntry("rihan", 29, From),
                MealEntry("safar", 64, From),
                MealEntry("tanvir", 32, From),
            ]);

        var result = await _sut.GetMealRateAsync("household-1", "caller-1", From, To);

        result.FoodCost.Should().Be(17000m);
        result.TotalMealUnits.Should().Be(292m);
        Math.Round(result.MealRate!.Value, 2).Should().Be(58.22m);
        result.Members.Sum(m => m.MealCost).Should().Be(17000m);
    }

    [Fact]
    public async Task GetMealRateAsync_ContributionCombinesPurchasesPaidAndDirectDeposits()
    {
        // Mirrors the spreadsheet's Ariyan wallet formula: purchases he personally paid for
        // (560 + 470) plus direct cash top-ups (2450 + 160 + 100 + 500) = 4240 total.
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Purchase("ariyan", 560m), Purchase("ariyan", 470m)]);
        _contributions.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Contribution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ContributionEntry("ariyan", 2450m), ContributionEntry("ariyan", 160m), ContributionEntry("ariyan", 100m), ContributionEntry("ariyan", 500m)]);
        _mealEntries.Setup(x => x.FindAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MealEntry("ariyan", 10, From)]);

        var result = await _sut.GetMealRateAsync("household-1", "caller-1", From, To);

        var ariyan = result.Members.Single(m => m.UserId == "ariyan");
        ariyan.Contribution.Should().Be(4240m);
        ariyan.GiveTake.Should().Be(ariyan.Contribution - ariyan.MealCost);
    }

    [Fact]
    public async Task GetMealRateAsync_MemberWithContributionButNoMeals_StillAppearsWithFullGiveTake()
    {
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Purchase("eater-1", 1000m)]);
        _contributions.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Contribution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ContributionEntry("payer-only", 500m)]);
        _mealEntries.Setup(x => x.FindAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MealEntry("eater-1", 10, From)]);

        var result = await _sut.GetMealRateAsync("household-1", "caller-1", From, To);

        var payerOnly = result.Members.Single(m => m.UserId == "payer-only");
        payerOnly.MealUnits.Should().Be(0m);
        payerOnly.MealCost.Should().Be(0m);
        payerOnly.Contribution.Should().Be(500m);
        payerOnly.GiveTake.Should().Be(500m);
    }

    [Fact]
    public async Task GetMealRateAsync_NoMealEntries_ReturnsNullRateAndNoMealCost()
    {
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Purchase("ariyan", 1000m)]);
        _mealEntries.Setup(x => x.FindAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetMealRateAsync("household-1", "caller-1", From, To);

        result.MealRate.Should().BeNull();
        result.Members.Single().MealCost.Should().Be(0m);
    }

    [Fact]
    public async Task GetMealRateAsync_ToBeforeFrom_ThrowsValidationException()
    {
        var act = () => _sut.GetMealRateAsync("household-1", "caller-1", To, From, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetMealRateAsync_UnevenSplit_MemberCostsStillSumToFoodCostExactly()
    {
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Purchase("a", 1000m)]);
        _mealEntries.Setup(x => x.FindAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                MealEntry("a", 1, From),
                MealEntry("b", 1, From),
                MealEntry("c", 1, From),
            ]);

        var result = await _sut.GetMealRateAsync("household-1", "caller-1", From, To);

        result.Members.Sum(m => m.MealCost).Should().Be(1000m);
    }
}
