using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.HouseholdBalance.Services;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.HouseholdBalance;

public class HouseholdBalanceServiceTests
{
    private static readonly DateTime Now = new(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<Contribution>> _contributions = new();
    private readonly Mock<IRepository<BazarPurchase>> _purchases = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly HouseholdBalanceService _sut;

    public HouseholdBalanceServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        var access = new HouseholdAccessService(_memberships.Object);
        _sut = new HouseholdBalanceService(_contributions.Object, _purchases.Object, access, _dateTimeProvider.Object);

        _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMembership
            {
                Id = "membership-1",
                HouseholdId = "household-1",
                UserId = "caller-1",
                Role = HouseholdRole.Member,
                Status = HouseholdMembershipStatus.Active,
            });
    }

    private static Contribution ContributionEntry(decimal amount) => new()
    {
        Id = Guid.NewGuid().ToString(),
        HouseholdId = "household-1",
        ContributedByUserId = "someone",
        Date = Today,
        Amount = amount,
        Currency = "BDT",
        Status = FinancialEntryStatus.Active,
    };

    private static BazarPurchase FundPurchase(decimal amount) => new()
    {
        Id = Guid.NewGuid().ToString(),
        HouseholdId = "household-1",
        PurchasedByUserId = "someone",
        Date = Today,
        Amount = amount,
        Currency = "BDT",
        FundingSource = BazarFundingSource.HouseholdFund,
        Status = FinancialEntryStatus.Active,
    };

    // Ariyan deposits 3000 (Contribution). Ariyan buys Bazar for 2000 out of pocket — Personal
    // funding, so it never reaches this repository query as a fund draw; it only mirrors as
    // another 3000+2000=5000 total Contribution (created by BazarPurchaseService, simulated here
    // directly as a Contribution row). Waythin then buys 2000 of Bazar FROM the fund. The balance
    // should land at 5000 - 2000 = 3000, matching the worked example in the feature request.
    [Fact]
    public async Task GetBalanceAsync_WorkedExample_MatchesExpectedBalance()
    {
        _contributions.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Contribution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ContributionEntry(3000m), ContributionEntry(2000m)]);
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([FundPurchase(2000m)]);

        var result = await _sut.GetBalanceAsync("household-1", "caller-1", CancellationToken.None);

        result.TotalContributions.Should().Be(5000m);
        result.TotalSpentFromFund.Should().Be(2000m);
        result.CurrentBalance.Should().Be(3000m);
    }

    [Fact]
    public async Task GetBalanceAsync_NoActivity_ReturnsZero()
    {
        _contributions.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Contribution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetBalanceAsync("household-1", "caller-1", CancellationToken.None);

        result.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    public async Task GetBalanceAsync_ByViewer_Succeeds()
    {
        // Balance is informational — every role including Viewer can read it.
        _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMembership
            {
                Id = "membership-2",
                HouseholdId = "household-1",
                UserId = "viewer-1",
                Role = HouseholdRole.Viewer,
                Status = HouseholdMembershipStatus.Active,
            });
        _contributions.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Contribution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _purchases.Setup(x => x.FindAsync(It.IsAny<Expression<Func<BazarPurchase, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _sut.GetBalanceAsync("household-1", "viewer-1", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetBalanceAsync_NonMember_ThrowsNotFound()
    {
        _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HouseholdMembership?)null);

        var act = () => _sut.GetBalanceAsync("household-1", "stranger-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
