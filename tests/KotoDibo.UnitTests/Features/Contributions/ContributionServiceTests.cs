using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Contributions.DTOs;
using KotoDibo.Application.Features.Contributions.Services;
using KotoDibo.Application.Features.Contributions.Validators;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using Moq;

namespace KotoDibo.UnitTests.Features.Contributions;

public class ContributionServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<Contribution>> _contributions = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly ContributionService _sut;

    public ContributionServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _contributions.Setup(x => x.AddAsync(It.IsAny<Contribution>(), It.IsAny<CancellationToken>()))
            .Callback<Contribution, CancellationToken>((c, _) => c.Id = "contribution-1")
            .ReturnsAsync((Contribution c, CancellationToken _) => c);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new ContributionService(
            _contributions.Object,
            access,
            _dateTimeProvider.Object,
            new CreateContributionRequestValidator(),
            new UpdateContributionRequestValidator());
    }

    private static HouseholdMembership Membership(HouseholdRole role, string userId) => new()
    {
        Id = "membership-1",
        HouseholdId = "household-1",
        UserId = userId,
        Role = role,
        Status = HouseholdMembershipStatus.Active,
        JoinedAt = Now,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private void GivenMembership(HouseholdMembership membership)
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

    private void GivenExistingContribution(Contribution contribution)
        => _contributions.Setup(x => x.GetByIdAsync(contribution.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contribution);

    private static Contribution ActiveContribution(string contributedByUserId, decimal amount = 500m) => new()
    {
        Id = "contribution-1",
        HouseholdId = "household-1",
        ContributedByUserId = contributedByUserId,
        Date = Today,
        Amount = amount,
        Currency = "BDT",
        Status = FinancialEntryStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    [Fact]
    public async Task CreateAsync_Valid_CreatesActiveContribution()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var result = await _sut.CreateAsync("household-1", "member-1", "member-1", new CreateContributionRequest
        {
            Date = Today,
            Amount = 2450m,
            Currency = "BDT",
            Notes = "Cash top-up",
        });

        result.Amount.Should().Be(2450m);
        result.ContributedByUserId.Should().Be("member-1");
        result.CreatedByUserId.Should().Be("member-1");
        result.Status.Should().Be(nameof(FinancialEntryStatus.Active));
    }

    [Fact]
    public async Task CreateAsync_ByManagerOnBehalfOfMember_OwnerIsTargetAndCreatorIsCaller()
    {
        GivenMembership(Membership(HouseholdRole.Manager, "manager-1"));

        var result = await _sut.CreateAsync("household-1", "manager-1", "member-2", new CreateContributionRequest
        {
            Date = Today,
            Amount = 3000m,
            Currency = "BDT",
            Notes = "Handed over in person",
        });

        result.ContributedByUserId.Should().Be("member-2");
        result.CreatedByUserId.Should().Be("manager-1");
    }

    [Fact]
    public async Task CreateAsync_ByMemberOnBehalfOfAnotherMember_ThrowsForbidden()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.CreateAsync("household-1", "member-1", "member-2", new CreateContributionRequest
        {
            Date = Today,
            Amount = 100m,
            Currency = "BDT",
        });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_FutureDate_ThrowsValidationException()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.CreateAsync("household-1", "member-1", "member-1", new CreateContributionRequest
        {
            Date = Today.AddDays(1),
            Amount = 100m,
            Currency = "BDT",
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_ByViewer_ThrowsForbidden()
    {
        GivenMembership(Membership(HouseholdRole.Viewer, "viewer-1"));

        var act = () => _sut.CreateAsync("household-1", "viewer-1", "viewer-1", new CreateContributionRequest
        {
            Date = Today,
            Amount = 100m,
            Currency = "BDT",
        });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_ByOtherMemberNotOwner_ThrowsForbidden()
    {
        var contribution = ActiveContribution(contributedByUserId: "payer-1");
        GivenExistingContribution(contribution);
        GivenMembership(Membership(HouseholdRole.Member, "member-2"));

        var act = () => _sut.UpdateAsync("household-1", "member-2", "contribution-1", new UpdateContributionRequest { Notes = "edit" }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_ByContributionOwner_Succeeds()
    {
        var contribution = ActiveContribution(contributedByUserId: "payer-1");
        GivenExistingContribution(contribution);
        GivenMembership(Membership(HouseholdRole.Member, "payer-1"));

        var result = await _sut.UpdateAsync("household-1", "payer-1", "contribution-1", new UpdateContributionRequest { Amount = 600m }, CancellationToken.None);

        result.Amount.Should().Be(600m);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ThrowsDomainException()
    {
        var contribution = ActiveContribution(contributedByUserId: "payer-1");
        contribution.Status = FinancialEntryStatus.Cancelled;
        GivenExistingContribution(contribution);
        GivenMembership(Membership(HouseholdRole.Member, "payer-1"));

        var act = () => _sut.CancelAsync("household-1", "payer-1", "contribution-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelAsync_ByOwner_SetsStatusCancelled()
    {
        var contribution = ActiveContribution(contributedByUserId: "payer-1");
        GivenExistingContribution(contribution);
        GivenMembership(Membership(HouseholdRole.Member, "payer-1"));

        var result = await _sut.CancelAsync("household-1", "payer-1", "contribution-1", CancellationToken.None);

        result.Status.Should().Be(nameof(FinancialEntryStatus.Cancelled));
    }

    [Fact]
    public async Task UpdateAsync_AutoGeneratedFromBazar_ThrowsDomainException()
    {
        // Auto-generated rows mirror a Bazar purchase — they must be edited via that purchase
        // (BazarPurchaseService) so the two records never drift apart.
        var contribution = ActiveContribution(contributedByUserId: "payer-1");
        contribution.SourceType = ContributionSourceType.AutoFromBazar;
        contribution.SourceBazarPurchaseId = "purchase-1";
        GivenExistingContribution(contribution);
        GivenMembership(Membership(HouseholdRole.Member, "payer-1"));

        var act = () => _sut.UpdateAsync("household-1", "payer-1", "contribution-1", new UpdateContributionRequest { Amount = 600m }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelAsync_AutoGeneratedFromBazar_ThrowsDomainException()
    {
        var contribution = ActiveContribution(contributedByUserId: "payer-1");
        contribution.SourceType = ContributionSourceType.AutoFromBazar;
        contribution.SourceBazarPurchaseId = "purchase-1";
        GivenExistingContribution(contribution);
        GivenMembership(Membership(HouseholdRole.Member, "payer-1"));

        var act = () => _sut.CancelAsync("household-1", "payer-1", "contribution-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
