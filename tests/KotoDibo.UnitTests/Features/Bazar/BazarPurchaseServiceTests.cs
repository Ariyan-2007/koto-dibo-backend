using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Application.Features.Bazar.Services;
using KotoDibo.Application.Features.Bazar.Validators;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using Moq;

namespace KotoDibo.UnitTests.Features.Bazar;

public class BazarPurchaseServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IRepository<BazarPurchase>> _purchases = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly BazarPurchaseService _sut;

    public BazarPurchaseServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _purchases.Setup(x => x.AddAsync(It.IsAny<BazarPurchase>(), It.IsAny<CancellationToken>()))
            .Callback<BazarPurchase, CancellationToken>((p, _) => p.Id = "purchase-1")
            .ReturnsAsync((BazarPurchase p, CancellationToken _) => p);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new BazarPurchaseService(
            _purchases.Object,
            access,
            _dateTimeProvider.Object,
            new CreateBazarPurchaseRequestValidator(),
            new UpdateBazarPurchaseRequestValidator());
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

    private void GivenExistingPurchase(BazarPurchase purchase)
        => _purchases.Setup(x => x.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

    private static BazarPurchase ActivePurchase(string purchasedByUserId, decimal amount = 500m) => new()
    {
        Id = "purchase-1",
        HouseholdId = "household-1",
        PurchasedByUserId = purchasedByUserId,
        Date = Today,
        Amount = amount,
        Currency = "BDT",
        Status = FinancialEntryStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    [Fact]
    public async Task CreateAsync_Valid_CreatesActivePurchase()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var result = await _sut.CreateAsync("household-1", "member-1", "member-1", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 560m,
            Currency = "BDT",
            Note = "Chicken",
        });

        result.Amount.Should().Be(560m);
        result.Note.Should().Be("Chicken");
        result.PurchasedByUserId.Should().Be("member-1");
        result.Status.Should().Be(nameof(FinancialEntryStatus.Active));
    }

    [Fact]
    public async Task CreateAsync_NegativeAmount_RecordsLeftoverEntry()
    {
        // Mirrors the real household's spreadsheet convention: an entry like "-700, Leftover"
        // records unspent shopping cash carried into next month, deflating this month's FoodCost.
        GivenMembership(Membership(HouseholdRole.Member, "ariyan"));

        var result = await _sut.CreateAsync("household-1", "ariyan", "ariyan", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = -700m,
            Currency = "BDT",
            Note = "Leftover",
        });

        result.Amount.Should().Be(-700m);
    }

    [Fact]
    public async Task CreateAsync_ZeroAmount_ThrowsValidationException()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.CreateAsync("household-1", "member-1", "member-1", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 0m,
            Currency = "BDT",
        });

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_DateIsTomorrowInUtcButTodayInBangladeshLocalTime_Succeeds()
    {
        // 19:00 UTC is already past local midnight in Bangladesh (UTC+6) — the household's local
        // "today" is one calendar day ahead of the raw UTC date at this moment.
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2026, 8, 31, 19, 0, 0, DateTimeKind.Utc));
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var result = await _sut.CreateAsync("household-1", "member-1", "member-1", new CreateBazarPurchaseRequest
        {
            Date = new DateOnly(2026, 9, 1),
            Amount = 100m,
            Currency = "BDT",
        });

        result.Date.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public async Task CreateAsync_FutureDate_ThrowsValidationException()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.CreateAsync("household-1", "member-1", "member-1", new CreateBazarPurchaseRequest
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

        var act = () => _sut.CreateAsync("household-1", "viewer-1", "viewer-1", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 100m,
            Currency = "BDT",
        });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_ByManagerForOtherMember_Succeeds()
    {
        GivenMembership(Membership(HouseholdRole.Manager, "manager-1"));

        var result = await _sut.CreateAsync("household-1", "manager-1", "member-2", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 300m,
            Currency = "BDT",
            Note = "Manager on behalf",
        });

        result.PurchasedByUserId.Should().Be("member-2");
    }

    [Fact]
    public async Task CreateAsync_ByMemberForOtherMember_ThrowsForbidden()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));

        var act = () => _sut.CreateAsync("household-1", "member-1", "member-2", new CreateBazarPurchaseRequest
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
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "member-2"));

        var act = () => _sut.UpdateAsync("household-1", "member-2", "purchase-1", new UpdateBazarPurchaseRequest { Note = "edit" }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_ByPurchaseOwner_Succeeds()
    {
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        var result = await _sut.UpdateAsync("household-1", "buyer-1", "purchase-1", new UpdateBazarPurchaseRequest { Amount = 750m }, CancellationToken.None);

        result.Amount.Should().Be(750m);
        _purchases.Verify(x => x.UpdateAsync(It.IsAny<BazarPurchase>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ByManager_CanEditAnyonesPurchase()
    {
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Manager, "manager-1"));

        var result = await _sut.UpdateAsync("household-1", "manager-1", "purchase-1", new UpdateBazarPurchaseRequest { Note = "Manager edit" }, CancellationToken.None);

        result.Note.Should().Be("Manager edit");
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ThrowsDomainException()
    {
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        purchase.Status = FinancialEntryStatus.Cancelled;
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        var act = () => _sut.CancelAsync("household-1", "buyer-1", "purchase-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelAsync_ByOwnerOfPurchase_SetsStatusCancelled()
    {
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        var result = await _sut.CancelAsync("household-1", "buyer-1", "purchase-1", CancellationToken.None);

        result.Status.Should().Be(nameof(FinancialEntryStatus.Cancelled));
    }
}
