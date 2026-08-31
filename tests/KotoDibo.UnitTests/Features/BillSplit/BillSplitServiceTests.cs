using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BillSplit.DTOs;
using KotoDibo.Application.Features.BillSplit.Services;
using KotoDibo.Application.Features.BillSplit.Validators;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using Moq;
using BillSplitEntity = KotoDibo.Domain.Entities.BillSplit;

namespace KotoDibo.UnitTests.Features.BillSplit;

public class BillSplitServiceTests
{
    private static readonly DateTime Now = new(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly PeriodFrom = new(2026, 1, 1);
    private static readonly DateOnly PeriodTo = new(2026, 1, 31);

    private readonly Mock<IRepository<BillSplitEntity>> _billSplits = new();
    private readonly Mock<IRepository<UtilityTariffConfig>> _tariffConfigs = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly BillSplitService _sut;

    public BillSplitServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _billSplits.Setup(x => x.AddAsync(It.IsAny<BillSplitEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BillSplitEntity, CancellationToken>((b, _) => b.Id = "billsplit-1")
            .ReturnsAsync((BillSplitEntity b, CancellationToken _) => b);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new BillSplitService(
            _billSplits.Object,
            _tariffConfigs.Object,
            _memberships.Object,
            access,
            _dateTimeProvider.Object,
            new CreateBillSplitRequestValidator(),
            new UpdateBillSplitRequestValidator());
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

    private void GivenCallerMembership(HouseholdMembership membership)
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

    private void GivenActiveMembers(params HouseholdMembership[] members)
        => _memberships.Setup(x => x.FindAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

    private void GivenExistingBillSplit(BillSplitEntity entity)
        => _billSplits.Setup(x => x.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

    private void GivenTariffConfig(UtilityTariffConfig config)
        => _tariffConfigs.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UtilityTariffConfig, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

    private static UtilityTariffConfig BangladeshLikeTariff() => new()
    {
        Id = "tariff-1",
        Country = "BD",
        Provider = "Residential",
        UtilityType = "Electricity",
        Currency = "BDT",
        IsActive = true,
        Bands =
        [
            new TariffBand { FromUnits = 0, ToUnits = 100, RatePerUnit = 5m },
            new TariffBand { FromUnits = 100, ToUnits = 400, RatePerUnit = 7m },
            new TariffBand { FromUnits = 400, ToUnits = null, RatePerUnit = 10m },
        ],
    };

    private static BillSplitEntity TariffMeteredBillSplit(string createdBy) => new()
    {
        Id = "billsplit-1",
        HouseholdId = "household-1",
        CreatedByUserId = createdBy,
        Title = "January electricity",
        SplitMethod = BillSplitMethod.TariffMetered,
        PeriodFrom = PeriodFrom,
        PeriodTo = PeriodTo,
        Currency = "BDT",
        TariffCountry = "BD",
        MainMeterUsage = 500m,
        MemberInputs = [new BillSplitMemberInput { UserId = "ariyan", Value = 350m }, new BillSplitMemberInput { UserId = "rihan", Value = 100m }],
        Status = FinancialEntryStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    [Fact]
    public async Task CreateAsync_TariffMeteredValid_CreatesActiveRecord()
    {
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));
        GivenActiveMembers(Membership(HouseholdRole.Member, "ariyan"), Membership(HouseholdRole.Member, "rihan"));
        GivenTariffConfig(BangladeshLikeTariff());

        var result = await _sut.CreateAsync("household-1", "ariyan", new CreateBillSplitRequest
        {
            Title = "January electricity",
            SplitMethod = "TariffMetered",
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            Currency = "bdt",
            TariffCountry = "bd",
            MainMeterUsage = 500m,
            MemberInputs = [new BillSplitMemberInputDto { UserId = "ariyan", Value = 350m }, new BillSplitMemberInputDto { UserId = "rihan", Value = 100m }],
        });

        result.Status.Should().Be(nameof(FinancialEntryStatus.Active));
        result.SplitMethod.Should().Be(nameof(BillSplitMethod.TariffMetered));
        result.Currency.Should().Be("BDT");
        result.TariffCountry.Should().Be("BD");
    }

    [Fact]
    public async Task CreateAsync_SubMeterSumExceedsMainMeterUsage_ThrowsFluentValidationException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));

        var act = () => _sut.CreateAsync("household-1", "ariyan", new CreateBillSplitRequest
        {
            Title = "Bad bill",
            SplitMethod = "TariffMetered",
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            Currency = "BDT",
            TariffCountry = "BD",
            MainMeterUsage = 100m,
            MemberInputs = [new BillSplitMemberInputDto { UserId = "ariyan", Value = 150m }],
        });

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_UnknownTariffCountry_ThrowsValidationException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));
        GivenActiveMembers(Membership(HouseholdRole.Member, "ariyan"));
        _tariffConfigs.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UtilityTariffConfig, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UtilityTariffConfig?)null);

        var act = () => _sut.CreateAsync("household-1", "ariyan", new CreateBillSplitRequest
        {
            Title = "Unknown tariff",
            SplitMethod = "TariffMetered",
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            Currency = "BDT",
            TariffCountry = "ZZ",
            MainMeterUsage = 100m,
            MemberInputs = [new BillSplitMemberInputDto { UserId = "ariyan", Value = 50m }],
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_MemberInputIncludesNonActiveMember_ThrowsValidationException()
    {
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));
        GivenActiveMembers(Membership(HouseholdRole.Member, "ariyan"));
        GivenTariffConfig(BangladeshLikeTariff());

        var act = () => _sut.CreateAsync("household-1", "ariyan", new CreateBillSplitRequest
        {
            Title = "January electricity",
            SplitMethod = "TariffMetered",
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            Currency = "BDT",
            TariffCountry = "BD",
            MainMeterUsage = 500m,
            MemberInputs = [new BillSplitMemberInputDto { UserId = "not-a-member", Value = 100m }],
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_ByViewer_ThrowsForbidden()
    {
        GivenCallerMembership(Membership(HouseholdRole.Viewer, "viewer-1"));

        var act = () => _sut.CreateAsync("household-1", "viewer-1", new CreateBillSplitRequest
        {
            Title = "Wifi",
            SplitMethod = "EqualSplit",
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            Currency = "BDT",
            TotalAmount = 1000m,
        });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetSettlementAsync_TariffMetered_MatchesFairSplitAllocatorWorkedExample()
    {
        var entity = TariffMeteredBillSplit("ariyan");
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));
        GivenExistingBillSplit(entity);
        GivenTariffConfig(BangladeshLikeTariff());
        GivenActiveMembers(
            Membership(HouseholdRole.Member, "ariyan"),
            Membership(HouseholdRole.Member, "rihan"),
            Membership(HouseholdRole.Member, "tanvir"));

        var result = await _sut.GetSettlementAsync("household-1", "ariyan", "billsplit-1");

        result.TotalAmount.Should().Be(3600m);
        result.AttributedCost.Should().Be(3350m);
        result.SharedCost.Should().Be(250m);
        result.Members.Sum(m => m.TotalOwed).Should().Be(result.TotalAmount);
        result.Members.Single(m => m.UserId == "tanvir").AttributedCost.Should().Be(0m);
    }

    [Fact]
    public async Task GetSettlementAsync_EqualSplit_DividesAcrossActiveMembers()
    {
        var entity = new BillSplitEntity
        {
            Id = "billsplit-2",
            HouseholdId = "household-1",
            CreatedByUserId = "ariyan",
            Title = "Wifi",
            SplitMethod = BillSplitMethod.EqualSplit,
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            Currency = "BDT",
            TotalAmount = 300m,
            Status = FinancialEntryStatus.Active,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));
        GivenExistingBillSplit(entity);
        GivenActiveMembers(
            Membership(HouseholdRole.Member, "ariyan"),
            Membership(HouseholdRole.Member, "rihan"),
            Membership(HouseholdRole.Member, "tanvir"));

        var result = await _sut.GetSettlementAsync("household-1", "ariyan", "billsplit-2");

        result.Members.Should().HaveCount(3);
        result.Members.Sum(m => m.TotalOwed).Should().Be(300m);
        result.Members.Should().OnlyContain(m => m.TotalOwed == 100m);
    }

    [Fact]
    public async Task UpdateAsync_ByNonCreatorMember_ThrowsForbidden()
    {
        var entity = TariffMeteredBillSplit("ariyan");
        GivenExistingBillSplit(entity);
        GivenCallerMembership(Membership(HouseholdRole.Member, "rihan"));

        var act = () => _sut.UpdateAsync("household-1", "rihan", "billsplit-1", new UpdateBillSplitRequest { Title = "edit" }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_ByCreator_Succeeds()
    {
        var entity = TariffMeteredBillSplit("ariyan");
        GivenExistingBillSplit(entity);
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));

        var result = await _sut.UpdateAsync("household-1", "ariyan", "billsplit-1", new UpdateBillSplitRequest { Title = "Updated title" }, CancellationToken.None);

        result.Title.Should().Be("Updated title");
    }

    [Fact]
    public async Task UpdateAsync_ByManager_CanEditAnyonesBillSplit()
    {
        var entity = TariffMeteredBillSplit("ariyan");
        GivenExistingBillSplit(entity);
        GivenCallerMembership(Membership(HouseholdRole.Manager, "manager-1"));

        var result = await _sut.UpdateAsync("household-1", "manager-1", "billsplit-1", new UpdateBillSplitRequest { Title = "Manager edit" }, CancellationToken.None);

        result.Title.Should().Be("Manager edit");
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ThrowsDomainException()
    {
        var entity = TariffMeteredBillSplit("ariyan");
        entity.Status = FinancialEntryStatus.Cancelled;
        GivenExistingBillSplit(entity);
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));

        var act = () => _sut.CancelAsync("household-1", "ariyan", "billsplit-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelAsync_ByCreator_SetsStatusCancelled()
    {
        var entity = TariffMeteredBillSplit("ariyan");
        GivenExistingBillSplit(entity);
        GivenCallerMembership(Membership(HouseholdRole.Member, "ariyan"));

        var result = await _sut.CancelAsync("household-1", "ariyan", "billsplit-1", CancellationToken.None);

        result.Status.Should().Be(nameof(FinancialEntryStatus.Cancelled));
    }
}
