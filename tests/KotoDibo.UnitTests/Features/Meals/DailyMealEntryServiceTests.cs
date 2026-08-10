using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Application.Features.Meals.DTOs;
using KotoDibo.Application.Features.Meals.Services;
using KotoDibo.Application.Features.Meals.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.Meals;

public class DailyMealEntryServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<DailyMealEntry>> _entries = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly DailyMealEntryService _sut;

    public DailyMealEntryServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _entries.Setup(x => x.AddAsync(It.IsAny<DailyMealEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DailyMealEntry, CancellationToken>((e, _) => e.Id = "entry-1")
            .ReturnsAsync((DailyMealEntry e, CancellationToken _) => e);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new DailyMealEntryService(_entries.Object, access, _dateTimeProvider.Object, new SetMealCountRequestValidator());
    }

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

    private void GivenMemberships(params HouseholdMembership[] candidates)
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<HouseholdMembership, bool>> predicate, CancellationToken _) =>
                candidates.FirstOrDefault(predicate.Compile()));

    private void GivenNoExistingEntry()
        => _entries.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyMealEntry?)null);

    [Fact]
    public async Task SetCountAsync_SelfNewEntry_CreatesEntry()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"));
        GivenNoExistingEntry();

        var result = await _sut.SetCountAsync("household-1", "member-1", "member-1", Today, new SetMealCountRequest { Count = 2m });

        result.UserId.Should().Be("member-1");
        result.Count.Should().Be(2m);
        _entries.Verify(x => x.AddAsync(It.IsAny<DailyMealEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetCountAsync_SelfExistingEntry_UpdatesCount()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"));
        var existing = new DailyMealEntry
        {
            Id = "entry-1",
            HouseholdId = "household-1",
            UserId = "member-1",
            Date = Today,
            Count = 1m,
            Status = DailyMealEntryStatus.Active,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        _entries.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.SetCountAsync("household-1", "member-1", "member-1", Today, new SetMealCountRequest { Count = 0.5m });

        result.Count.Should().Be(0.5m);
        _entries.Verify(x => x.AddAsync(It.IsAny<DailyMealEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _entries.Verify(x => x.UpdateAsync(It.IsAny<DailyMealEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetCountAsync_ForOtherUserByPlainMember_ThrowsForbidden()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"), Membership(HouseholdRole.Member, "member-2"));

        var act = () => _sut.SetCountAsync("household-1", "member-1", "member-2", Today, new SetMealCountRequest { Count = 1m });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task SetCountAsync_ForOtherUserByManager_Succeeds()
    {
        GivenMemberships(Membership(HouseholdRole.Manager, "manager-1"), Membership(HouseholdRole.Member, "member-2"));
        GivenNoExistingEntry();

        var result = await _sut.SetCountAsync("household-1", "manager-1", "member-2", Today, new SetMealCountRequest { Count = 1m });

        result.UserId.Should().Be("member-2");
    }

    [Fact]
    public async Task SetCountAsync_FutureDate_ThrowsValidationException()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.SetCountAsync("household-1", "member-1", "member-1", Today.AddDays(1), new SetMealCountRequest { Count = 1m });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SetCountAsync_CountAboveMax_ThrowsValidationException()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.SetCountAsync("household-1", "member-1", "member-1", Today, new SetMealCountRequest { Count = 6m });

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task SetCountAsync_TargetNotHouseholdMember_ThrowsNotFound()
    {
        GivenMemberships(Membership(HouseholdRole.Manager, "manager-1"));

        var act = () => _sut.SetCountAsync("household-1", "manager-1", "member-2", Today, new SetMealCountRequest { Count = 1m });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveAsync_ExistingEntry_SetsStatusRemoved()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"));
        var existing = new DailyMealEntry
        {
            Id = "entry-1",
            HouseholdId = "household-1",
            UserId = "member-1",
            Date = Today,
            Count = 1m,
            Status = DailyMealEntryStatus.Active,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        _entries.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<DailyMealEntry, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _sut.RemoveAsync("household-1", "member-1", "member-1", Today, CancellationToken.None);

        _entries.Verify(x => x.UpdateAsync(It.Is<DailyMealEntry>(e => e.Status == DailyMealEntryStatus.Removed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_NoExistingEntry_IsNoOp()
    {
        GivenMemberships(Membership(HouseholdRole.Member, "member-1"));
        GivenNoExistingEntry();

        await _sut.RemoveAsync("household-1", "member-1", "member-1", Today, CancellationToken.None);

        _entries.Verify(x => x.UpdateAsync(It.IsAny<DailyMealEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
