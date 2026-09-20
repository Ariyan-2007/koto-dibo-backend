using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Application.Features.Withdrawals.DTOs;
using KotoDibo.Application.Features.Withdrawals.Services;
using KotoDibo.Application.Features.Withdrawals.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using Moq;

namespace KotoDibo.UnitTests.Features.Withdrawals;

public class WithdrawalServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<Withdrawal>> _withdrawals = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IHouseholdBalanceService> _balance = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly WithdrawalService _sut;

    public WithdrawalServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _withdrawals.Setup(x => x.AddAsync(It.IsAny<Withdrawal>(), It.IsAny<CancellationToken>()))
            .Callback<Withdrawal, CancellationToken>((w, _) => w.Id = "withdrawal-1")
            .ReturnsAsync((Withdrawal w, CancellationToken _) => w);
        _balance.Setup(x => x.GetEstablishedCurrencyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("BDT");
        _balance.Setup(x => x.GetCurrentBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        _sut = new WithdrawalService(
            _withdrawals.Object,
            new HouseholdAccessService(_memberships.Object),
            _balance.Object,
            _dateTimeProvider.Object,
            new CreateWithdrawalRequestValidator());
    }

    private void GivenMembership(HouseholdRole role, string userId = "caller-1")
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMembership
            {
                Id = "membership-1",
                HouseholdId = "household-1",
                UserId = userId,
                Role = role,
                Status = HouseholdMembershipStatus.Active,
            });

    private static CreateWithdrawalRequest Request(decimal amount = 400m, string currency = "BDT") => new()
    {
        Date = Today,
        Amount = amount,
        Currency = currency,
    };

    [Fact]
    public async Task CreateAsync_WithinBalance_RecordsWithdrawal()
    {
        GivenMembership(HouseholdRole.Member);

        var result = await _sut.CreateAsync("household-1", "caller-1", "caller-1", Request(1000m), CancellationToken.None);

        result.Amount.Should().Be(1000m);
        result.WithdrawnByUserId.Should().Be("caller-1");
        _withdrawals.Verify(x => x.AddAsync(It.IsAny<Withdrawal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ExceedingBalance_ThrowsInsufficientFunds()
    {
        GivenMembership(HouseholdRole.Member);

        var act = () => _sut.CreateAsync("household-1", "caller-1", "caller-1", Request(1000.01m), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientFundsException>();
        _withdrawals.Verify(x => x.AddAsync(It.IsAny<Withdrawal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NoEstablishedCurrency_ThrowsInsufficientFunds()
    {
        GivenMembership(HouseholdRole.Member);
        _balance.Setup(x => x.GetEstablishedCurrencyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var act = () => _sut.CreateAsync("household-1", "caller-1", "caller-1", Request(), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task CreateAsync_WrongCurrency_ThrowsValidation()
    {
        GivenMembership(HouseholdRole.Member);

        var act = () => _sut.CreateAsync("household-1", "caller-1", "caller-1", Request(currency: "USD"), CancellationToken.None);

        await act.Should().ThrowAsync<KotoDibo.Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_ByViewer_ThrowsForbidden()
    {
        GivenMembership(HouseholdRole.Viewer);

        var act = () => _sut.CreateAsync("household-1", "caller-1", "caller-1", Request(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_MemberOnBehalfOfOther_ThrowsForbidden()
    {
        GivenMembership(HouseholdRole.Member);

        var act = () => _sut.CreateAsync("household-1", "caller-1", "other-1", Request(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_ManagerOnBehalfOfOther_RecordsAgainstTarget()
    {
        GivenMembership(HouseholdRole.Manager);

        var result = await _sut.CreateAsync("household-1", "caller-1", "other-1", Request(), CancellationToken.None);

        result.WithdrawnByUserId.Should().Be("other-1");
        result.CreatedByUserId.Should().Be("caller-1");
    }

    [Fact]
    public async Task DeleteAsync_MemberDeletingOthersWithdrawal_ThrowsForbidden()
    {
        GivenMembership(HouseholdRole.Member);
        _withdrawals.Setup(x => x.GetByIdAsync("withdrawal-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Withdrawal { Id = "withdrawal-1", HouseholdId = "household-1", WithdrawnByUserId = "other-1" });

        var act = () => _sut.DeleteAsync("household-1", "caller-1", "withdrawal-1", CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteAsync_OwnWithdrawal_Deletes()
    {
        GivenMembership(HouseholdRole.Member);
        _withdrawals.Setup(x => x.GetByIdAsync("withdrawal-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Withdrawal { Id = "withdrawal-1", HouseholdId = "household-1", WithdrawnByUserId = "caller-1" });

        await _sut.DeleteAsync("household-1", "caller-1", "withdrawal-1", CancellationToken.None);

        _withdrawals.Verify(x => x.DeleteAsync("withdrawal-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
