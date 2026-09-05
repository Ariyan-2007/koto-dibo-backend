using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Application.Features.Bazar.Services;
using KotoDibo.Application.Features.Bazar.Validators;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
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
    private readonly Mock<IRepository<Contribution>> _contributions = new();
    private readonly Mock<IHouseholdBalanceService> _householdBalanceService = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly BazarPurchaseService _sut;

    public BazarPurchaseServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _purchases.Setup(x => x.AddAsync(It.IsAny<BazarPurchase>(), It.IsAny<CancellationToken>()))
            .Callback<BazarPurchase, CancellationToken>((p, _) => p.Id = "purchase-1")
            .ReturnsAsync((BazarPurchase p, CancellationToken _) => p);
        _contributions.Setup(x => x.AddAsync(It.IsAny<Contribution>(), It.IsAny<CancellationToken>()))
            .Callback<Contribution, CancellationToken>((c, _) => c.Id = "mirrored-contribution-1")
            .ReturnsAsync((Contribution c, CancellationToken _) => c);
        _householdBalanceService.Setup(x => x.GetCurrentBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new BazarPurchaseService(
            _purchases.Object,
            _contributions.Object,
            _householdBalanceService.Object,
            access,
            _dateTimeProvider.Object,
            new TestHelpers.PassthroughUnitOfWork(),
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
    public async Task CreateAsync_CurrencyMismatchesHouseholdsEstablishedCurrency_ThrowsValidationException()
    {
        GivenMembership(Membership(HouseholdRole.Member, "member-1"));
        _householdBalanceService.Setup(x => x.GetEstablishedCurrencyAsync("household-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("BDT");

        var act = () => _sut.CreateAsync("household-1", "member-1", "member-1", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 100m,
            Currency = "USD",
        });

        await act.Should().ThrowAsync<ValidationException>();
        _purchases.Verify(x => x.AddAsync(It.IsAny<BazarPurchase>(), It.IsAny<CancellationToken>()), Times.Never);
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
        result.CreatedByUserId.Should().Be("manager-1");
    }

    [Fact]
    public async Task CreateAsync_PersonalFundingOnBehalfOfMember_MirroredContributionCreatedByIsTheRecorder()
    {
        // The mirrored Contribution's financial owner is the buyer (member-2), but its
        // CreatedByUserId must trace back to whoever actually submitted the Bazar entry
        // (manager-1) — not silently overwritten with the buyer's identity.
        GivenMembership(Membership(HouseholdRole.Manager, "manager-1"));

        await _sut.CreateAsync("household-1", "manager-1", "member-2", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 400m,
            Currency = "BDT",
            FundingSource = nameof(BazarFundingSource.Personal),
        });

        _contributions.Verify(x => x.AddAsync(
            It.Is<Contribution>(c => c.ContributedByUserId == "member-2" && c.CreatedByUserId == "manager-1"),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task DeleteAsync_LegacyCancelledRow_StillDeletes()
    {
        // Deletion is a hard delete now — there's no "already cancelled" state that blocks it;
        // even a pre-existing soft-cancelled row from before this change can be permanently removed.
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        purchase.Status = FinancialEntryStatus.Cancelled;
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        await _sut.DeleteAsync("household-1", "buyer-1", "purchase-1", CancellationToken.None);

        _purchases.Verify(x => x.DeleteAsync("purchase-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ByOwnerOfPurchase_RemovesPurchase()
    {
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        await _sut.DeleteAsync("household-1", "buyer-1", "purchase-1", CancellationToken.None);

        _purchases.Verify(x => x.DeleteAsync("purchase-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_PersonalFunding_AutoCreatesMirroredContribution()
    {
        // The core accounting rule: paying for Bazar out of pocket is, from the household's point
        // of view, indistinguishable from depositing that same amount and immediately spending it.
        GivenMembership(Membership(HouseholdRole.Member, "ariyan"));

        var result = await _sut.CreateAsync("household-1", "ariyan", "ariyan", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 2000m,
            Currency = "BDT",
            FundingSource = nameof(BazarFundingSource.Personal),
        });

        result.FundingSource.Should().Be(nameof(BazarFundingSource.Personal));
        result.LinkedContributionId.Should().Be("mirrored-contribution-1");
        _contributions.Verify(x => x.AddAsync(
            It.Is<Contribution>(c => c.ContributedByUserId == "ariyan" && c.Amount == 2000m && c.SourceType == ContributionSourceType.AutoFromBazar && c.SourceBazarPurchaseId == "purchase-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_HouseholdFundFunding_WithSufficientBalance_DoesNotCreateContribution()
    {
        // Waythin drawing 2000 out of an existing 3000 balance: the fund is depleted, but Waythin
        // never personally handed the household any money, so no mirrored Contribution is created.
        _householdBalanceService.Setup(x => x.GetCurrentBalanceAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(3000m);
        GivenMembership(Membership(HouseholdRole.Member, "waythin"));

        var result = await _sut.CreateAsync("household-1", "waythin", "waythin", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 2000m,
            Currency = "BDT",
            FundingSource = nameof(BazarFundingSource.HouseholdFund),
        });

        result.FundingSource.Should().Be(nameof(BazarFundingSource.HouseholdFund));
        result.LinkedContributionId.Should().BeNull();
        _contributions.Verify(x => x.AddAsync(It.IsAny<Contribution>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_HouseholdFundFunding_ExceedsBalance_ThrowsInsufficientFunds()
    {
        _householdBalanceService.Setup(x => x.GetCurrentBalanceAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(1000m);
        GivenMembership(Membership(HouseholdRole.Member, "waythin"));

        var act = () => _sut.CreateAsync("household-1", "waythin", "waythin", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = 2000m,
            Currency = "BDT",
            FundingSource = nameof(BazarFundingSource.HouseholdFund),
        });

        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task CreateAsync_NegativeAmountWithHouseholdFund_ThrowsValidationException()
    {
        GivenMembership(Membership(HouseholdRole.Member, "ariyan"));

        var act = () => _sut.CreateAsync("household-1", "ariyan", "ariyan", new CreateBazarPurchaseRequest
        {
            Date = Today,
            Amount = -700m,
            Currency = "BDT",
            FundingSource = nameof(BazarFundingSource.HouseholdFund),
        });

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_PersonalFundedPurchase_AmountChange_ReconcilesLinkedContributionInPlace_NoDuplicate()
    {
        // The worked example from the spec: Bazar 1000 -> 1500 must update the same mirrored
        // Contribution to 1500 in place, never create a second one and never leave the old 1000
        // amount stale on either record.
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1", amount: 1000m);
        purchase.FundingSource = BazarFundingSource.Personal;
        purchase.LinkedContributionId = "mirrored-contribution-1";
        var linkedContribution = new Contribution
        {
            Id = "mirrored-contribution-1",
            HouseholdId = "household-1",
            ContributedByUserId = "buyer-1",
            CreatedByUserId = "buyer-1",
            Amount = 1000m,
            Currency = "BDT",
            Date = Today,
            SourceType = ContributionSourceType.AutoFromBazar,
            SourceBazarPurchaseId = "purchase-1",
            Status = FinancialEntryStatus.Active,
        };
        GivenExistingPurchase(purchase);
        _contributions.Setup(x => x.GetByIdAsync("mirrored-contribution-1", It.IsAny<CancellationToken>())).ReturnsAsync(linkedContribution);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        var result = await _sut.UpdateAsync("household-1", "buyer-1", "purchase-1", new UpdateBazarPurchaseRequest { Amount = 1500m }, CancellationToken.None);

        result.Amount.Should().Be(1500m);
        result.LinkedContributionId.Should().Be("mirrored-contribution-1");
        _contributions.Verify(x => x.AddAsync(It.IsAny<Contribution>(), It.IsAny<CancellationToken>()), Times.Never);
        _contributions.Verify(x => x.UpdateAsync(
            It.Is<Contribution>(c => c.Id == "mirrored-contribution-1" && c.Amount == 1500m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SwitchFromPersonalToHouseholdFund_DeletesNoLongerNeededLinkedContribution()
    {
        // Switching funding source away from Personal means the mirror no longer represents
        // anything real — it must be deleted outright (not soft-cancelled) as part of reconciling
        // the purchase's new state.
        _householdBalanceService.Setup(x => x.GetCurrentBalanceAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(5000m);
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1", amount: 1000m);
        purchase.FundingSource = BazarFundingSource.Personal;
        purchase.LinkedContributionId = "mirrored-contribution-1";
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        var result = await _sut.UpdateAsync("household-1", "buyer-1", "purchase-1", new UpdateBazarPurchaseRequest { FundingSource = nameof(BazarFundingSource.HouseholdFund) }, CancellationToken.None);

        result.LinkedContributionId.Should().BeNull();
        _contributions.Verify(x => x.DeleteAsync("mirrored-contribution-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_PersonalFundedPurchase_CascadeDeletesLinkedContribution()
    {
        var purchase = ActivePurchase(purchasedByUserId: "buyer-1");
        purchase.LinkedContributionId = "mirrored-contribution-1";
        GivenExistingPurchase(purchase);
        GivenMembership(Membership(HouseholdRole.Member, "buyer-1"));

        await _sut.DeleteAsync("household-1", "buyer-1", "purchase-1", CancellationToken.None);

        _contributions.Verify(x => x.DeleteAsync("mirrored-contribution-1", It.IsAny<CancellationToken>()), Times.Once);
        _purchases.Verify(x => x.DeleteAsync("purchase-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
