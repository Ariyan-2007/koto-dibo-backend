using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Application.Features.Households.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using Moq;

namespace KotoDibo.UnitTests.Features.Households;

public class HouseholdServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<Household>> _households = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly HouseholdService _sut;

    public HouseholdServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);

        _households.Setup(x => x.AddAsync(It.IsAny<Household>(), It.IsAny<CancellationToken>()))
            .Callback<Household, CancellationToken>((h, _) => h.Id = "household-1")
            .ReturnsAsync((Household h, CancellationToken _) => h);
        _memberships.Setup(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()))
            .Callback<HouseholdMembership, CancellationToken>((m, _) => m.Id = "membership-1")
            .ReturnsAsync((HouseholdMembership m, CancellationToken _) => m);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new HouseholdService(
            _households.Object,
            _memberships.Object,
            access,
            _dateTimeProvider.Object,
            new CreateHouseholdRequestValidator(),
            new UpdateHouseholdRequestValidator());
    }

    private static Household ActiveHousehold(string id = "household-1", string ownerId = "owner-1") => new()
    {
        Id = id,
        Name = "Test House",
        Status = HouseholdStatus.Active,
        OwnerUserId = ownerId,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static HouseholdMembership Membership(HouseholdRole role, string householdId = "household-1", string userId = "owner-1") => new()
    {
        Id = "membership-1",
        HouseholdId = householdId,
        UserId = userId,
        Role = role,
        Status = HouseholdMembershipStatus.Active,
        JoinedAt = Now,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private void GivenNoMembership()
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HouseholdMembership?)null);

    private void GivenMembership(HouseholdMembership membership)
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

    private void GivenActiveMemberCount(int count)
        => _memberships.Setup(x => x.FindAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, count).Select(_ => Membership(HouseholdRole.Member)).ToList());

    [Fact]
    public async Task CreateAsync_CreatesHouseholdAndOwnerMembership()
    {
        var request = new CreateHouseholdRequest { Name = "Sunny Apartment", Description = "3 people", Type = "SharedApartment" };

        var result = await _sut.CreateAsync("owner-1", request);

        result.Name.Should().Be("Sunny Apartment");
        result.OwnerUserId.Should().Be("owner-1");
        result.CallerRole.Should().Be(nameof(HouseholdRole.Owner));
        result.MemberCount.Should().Be(1);

        _households.Verify(x => x.AddAsync(It.IsAny<Household>(), It.IsAny<CancellationToken>()), Times.Once);
        _memberships.Verify(x => x.AddAsync(It.Is<HouseholdMembership>(m => m.Role == HouseholdRole.Owner && m.UserId == "owner-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_CallerNotAMember_ThrowsNotFound()
    {
        GivenNoMembership();

        var act = () => _sut.GetByIdAsync("household-1", "stranger", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ByManager_UpdatesFields()
    {
        var household = ActiveHousehold();
        GivenMembership(Membership(HouseholdRole.Manager, userId: "manager-1"));
        GivenActiveMemberCount(2);
        _households.Setup(x => x.GetByIdAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(household);

        var result = await _sut.UpdateAsync("household-1", "manager-1", new UpdateHouseholdRequest { Name = "Renamed House" });

        result.Name.Should().Be("Renamed House");
        _households.Verify(x => x.UpdateAsync(It.Is<Household>(h => h.Name == "Renamed House"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ByMember_ThrowsForbidden()
    {
        GivenMembership(Membership(HouseholdRole.Member, userId: "member-1"));

        var act = () => _sut.UpdateAsync("household-1", "member-1", new UpdateHouseholdRequest { Name = "X" }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_ArchivedHousehold_ThrowsDomainException()
    {
        var household = ActiveHousehold();
        household.Status = HouseholdStatus.Archived;
        GivenMembership(Membership(HouseholdRole.Owner));
        _households.Setup(x => x.GetByIdAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(household);

        var act = () => _sut.UpdateAsync("household-1", "owner-1", new UpdateHouseholdRequest { Name = "X" }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ArchiveAsync_ByManager_ThrowsForbidden()
    {
        GivenMembership(Membership(HouseholdRole.Manager, userId: "manager-1"));

        var act = () => _sut.ArchiveAsync("household-1", "manager-1", CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ArchiveAsync_ByOwner_ArchivesHousehold()
    {
        var household = ActiveHousehold();
        GivenMembership(Membership(HouseholdRole.Owner));
        GivenActiveMemberCount(1);
        _households.Setup(x => x.GetByIdAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(household);

        var result = await _sut.ArchiveAsync("household-1", "owner-1", CancellationToken.None);

        result.Status.Should().Be(nameof(HouseholdStatus.Archived));
        _households.Verify(x => x.UpdateAsync(It.Is<Household>(h => h.Status == HouseholdStatus.Archived && h.ArchivedAt == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreAsync_HouseholdNotArchived_ThrowsDomainException()
    {
        var household = ActiveHousehold();
        GivenMembership(Membership(HouseholdRole.Owner));
        _households.Setup(x => x.GetByIdAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(household);

        var act = () => _sut.RestoreAsync("household-1", "owner-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
