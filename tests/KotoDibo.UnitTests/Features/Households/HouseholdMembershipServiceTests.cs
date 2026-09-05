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
using KotoDibo.UnitTests.TestHelpers;
using Moq;

namespace KotoDibo.UnitTests.Features.Households;

public class HouseholdMembershipServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<Household>> _households = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IRepository<User>> _users = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly HouseholdMembershipService _sut;

    public HouseholdMembershipServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);

        _households.Setup(x => x.GetByIdAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveHousehold());
        _memberships.Setup(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()))
            .Callback<HouseholdMembership, CancellationToken>((m, _) => m.Id = "membership-new")
            .ReturnsAsync((HouseholdMembership m, CancellationToken _) => m);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new HouseholdMembershipService(
            _households.Object,
            _memberships.Object,
            _users.Object,
            access,
            _dateTimeProvider.Object,
            new PassthroughUnitOfWork(),
            new AddMemberRequestValidator(),
            new UpdateMemberRoleRequestValidator(),
            new TransferOwnershipRequestValidator());
    }

    private static Household ActiveHousehold() => new()
    {
        Id = "household-1",
        Name = "Test House",
        Status = HouseholdStatus.Active,
        OwnerUserId = "owner-1",
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static HouseholdMembership Membership(HouseholdRole role, string userId) => new()
    {
        Id = $"membership-{userId}",
        HouseholdId = "household-1",
        UserId = userId,
        Role = role,
        Status = HouseholdMembershipStatus.Active,
        JoinedAt = Now,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static User ExistingUser(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email,
        Name = "Some User",
        Status = AccountStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    // FindOneAsync is called with different predicates for different purposes (caller's own
    // membership, a target's membership, "is this user already an active member"). Rather than a
    // flat It.IsAny stub returning one fixed value for every call, actually evaluate the compiled
    // predicate against each candidate so each call resolves independently, like a real query would.
    private void GivenMemberships(params HouseholdMembership[] candidates)
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<HouseholdMembership, bool>> predicate, CancellationToken _) =>
                candidates.FirstOrDefault(predicate.Compile()));

    private void GivenCallerMembership(HouseholdMembership callerMembership) => GivenMemberships(callerMembership);

    [Fact]
    public async Task AddMemberAsync_NewUser_CreatesActiveMembership()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));
        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser("new-user", "new@example.com"));

        var result = await _sut.AddMemberAsync("household-1", "owner-1", new AddMemberRequest { Email = "new@example.com", Role = "Member" });

        result.UserId.Should().Be("new-user");
        result.Role.Should().Be(nameof(HouseholdRole.Member));
        _memberships.Verify(x => x.AddAsync(It.Is<HouseholdMembership>(m => m.UserId == "new-user" && m.Role == HouseholdRole.Member), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_UnknownEmail_ThrowsValidationException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));
        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.AddMemberAsync("household-1", "owner-1", new AddMemberRequest { Email = "nobody@example.com", Role = "Member" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AddMemberAsync_TargetAlreadyActiveMember_ThrowsValidationException()
    {
        var existingTarget = ExistingUser("existing-member", "existing@example.com");
        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTarget);

        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"), Membership(HouseholdRole.Member, "existing-member"));

        var act = () => _sut.AddMemberAsync("household-1", "owner-1", new AddMemberRequest { Email = "existing@example.com", Role = "Member" });

        await act.Should().ThrowAsync<ValidationException>();
        _memberships.Verify(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMemberAsync_ByViewer_ThrowsForbidden()
    {
        GivenCallerMembership(Membership(HouseholdRole.Viewer, "viewer-1"));

        var act = () => _sut.AddMemberAsync("household-1", "viewer-1", new AddMemberRequest { Email = "someone@example.com", Role = "Member" });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_TargetIsOwner_ThrowsDomainException()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"), Membership(HouseholdRole.Manager, "manager-1"));

        var act = () => _sut.RemoveMemberAsync("household-1", "manager-1", "owner-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_ManagerRemovingAnotherManager_ThrowsForbidden()
    {
        GivenMemberships(Membership(HouseholdRole.Manager, "manager-1"), Membership(HouseholdRole.Manager, "manager-2"));

        var act = () => _sut.RemoveMemberAsync("household-1", "manager-1", "manager-2", CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_OwnerRemovingManager_Succeeds()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"), Membership(HouseholdRole.Manager, "manager-1"));

        await _sut.RemoveMemberAsync("household-1", "owner-1", "manager-1", CancellationToken.None);

        _memberships.Verify(x => x.UpdateAsync(
            It.Is<HouseholdMembership>(m => m.UserId == "manager-1" && m.Status == HouseholdMembershipStatus.Removed && m.RemovedBy == "owner-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_TargetIsSelf_ThrowsDomainException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));

        var act = () => _sut.UpdateMemberRoleAsync("household-1", "owner-1", "owner-1", new UpdateMemberRoleRequest { Role = "Manager" }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_TargetIsOwner_ThrowsDomainException()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"), Membership(HouseholdRole.Owner, "owner-2"));

        var act = () => _sut.UpdateMemberRoleAsync("household-1", "owner-1", "owner-2", new UpdateMemberRoleRequest { Role = "Manager" }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task TransferOwnershipAsync_ToActiveMember_PromotesTargetAndDemotesCaller()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"), Membership(HouseholdRole.Member, "member-1"));
        _users.Setup(x => x.GetByIdAsync("member-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser("member-1", "member@example.com"));

        var result = await _sut.TransferOwnershipAsync("household-1", "owner-1", new TransferOwnershipRequest { NewOwnerUserId = "member-1" }, CancellationToken.None);

        result.UserId.Should().Be("member-1");
        result.Role.Should().Be(nameof(HouseholdRole.Owner));
        _households.Verify(x => x.UpdateAsync(It.Is<Household>(h => h.OwnerUserId == "member-1"), It.IsAny<CancellationToken>()), Times.Once);
        _memberships.Verify(x => x.UpdateAsync(It.Is<HouseholdMembership>(m => m.UserId == "member-1" && m.Role == HouseholdRole.Owner), It.IsAny<CancellationToken>()), Times.Once);
        _memberships.Verify(x => x.UpdateAsync(It.Is<HouseholdMembership>(m => m.UserId == "owner-1" && m.Role == HouseholdRole.Manager), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferOwnershipAsync_ToSelf_ThrowsDomainException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));

        var act = () => _sut.TransferOwnershipAsync("household-1", "owner-1", new TransferOwnershipRequest { NewOwnerUserId = "owner-1" }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task TransferOwnershipAsync_ByNonOwner_ThrowsForbidden()
    {
        GivenCallerMembership(Membership(HouseholdRole.Manager, "manager-1"));

        var act = () => _sut.TransferOwnershipAsync("household-1", "manager-1", new TransferOwnershipRequest { NewOwnerUserId = "member-1" }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task TransferOwnershipAsync_TargetNotActiveMember_ThrowsNotFound()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));

        var act = () => _sut.TransferOwnershipAsync("household-1", "owner-1", new TransferOwnershipRequest { NewOwnerUserId = "not-a-member" }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task LeaveAsync_OwnerWithOtherActiveMembers_ThrowsDomainException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));
        _memberships.Setup(x => x.FindAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Membership(HouseholdRole.Member, "member-1")]);

        var act = () => _sut.LeaveAsync("household-1", "owner-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _households.Verify(x => x.UpdateAsync(It.IsAny<Household>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveAsync_OwnerIsSoleActiveMember_ArchivesHouseholdAndLeaves()
    {
        GivenCallerMembership(Membership(HouseholdRole.Owner, "owner-1"));
        _memberships.Setup(x => x.FindAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.LeaveAsync("household-1", "owner-1", CancellationToken.None);

        _households.Verify(x => x.UpdateAsync(It.Is<Household>(h => h.Status == HouseholdStatus.Archived), It.IsAny<CancellationToken>()), Times.Once);
        _memberships.Verify(x => x.UpdateAsync(It.Is<HouseholdMembership>(m => m.UserId == "owner-1" && m.Status == HouseholdMembershipStatus.Left), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveAsync_RegularMember_JustLeavesWithoutTouchingHousehold()
    {
        GivenCallerMembership(Membership(HouseholdRole.Member, "member-1"));

        await _sut.LeaveAsync("household-1", "member-1", CancellationToken.None);

        _households.Verify(x => x.UpdateAsync(It.IsAny<Household>(), It.IsAny<CancellationToken>()), Times.Never);
        _memberships.Verify(x => x.UpdateAsync(It.Is<HouseholdMembership>(m => m.UserId == "member-1" && m.Status == HouseholdMembershipStatus.Left), It.IsAny<CancellationToken>()), Times.Once);
    }
}
