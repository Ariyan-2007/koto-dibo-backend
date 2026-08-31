using FluentAssertions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BillSplit.DTOs;
using KotoDibo.Application.Features.BillSplit.Interfaces;
using KotoDibo.Application.Features.MealCalculation.DTOs;
using KotoDibo.Application.Features.MealCalculation.Interfaces;
using KotoDibo.Application.Features.Settlement.Services;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.Settlement;

public class SettlementServiceTests
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 1, 31);

    private readonly Mock<IMealCalculationService> _mealCalculationService = new();
    private readonly Mock<IBillSplitService> _billSplitService = new();
    private readonly Mock<IHouseholdAccessService> _access = new();

    private readonly SettlementService _sut;

    public SettlementServiceTests()
    {
        _access.Setup(x => x.RequireMembershipAsync("household-1", "caller-1", HouseholdPermission.ViewSettlement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMembership { HouseholdId = "household-1", UserId = "caller-1", Role = HouseholdRole.Member, Status = HouseholdMembershipStatus.Active });

        _sut = new SettlementService(_mealCalculationService.Object, _billSplitService.Object, _access.Object);
    }

    private void GivenMealResult(params MealMemberCostDto[] members)
        => _mealCalculationService.Setup(x => x.GetMealRateAsync("household-1", "caller-1", From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealCalculationDto { From = From, To = To, Members = members });

    private void GivenActiveBillSplits(params BillSplitDto[] billSplits)
        => _billSplitService.Setup(x => x.GetListAsync("household-1", "caller-1", From, To, nameof(FinancialEntryStatus.Active), It.IsAny<CancellationToken>()))
            .ReturnsAsync(billSplits);

    private void GivenBillSplitSettlement(string billSplitId, params BillSplitMemberSettlementDto[] members)
        => _billSplitService.Setup(x => x.GetSettlementAsync("household-1", "caller-1", billSplitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillSplitSettlementDto { BillSplitId = billSplitId, Members = members });

    [Fact]
    public async Task GetSettlementAsync_CombinesMealGiveTakeAndBillSplitOwed_NetsCorrectly()
    {
        GivenMealResult(
            new MealMemberCostDto { UserId = "ariyan", GiveTake = 500m },
            new MealMemberCostDto { UserId = "rihan", GiveTake = -500m });
        GivenActiveBillSplits(new BillSplitDto { Id = "bs-1", HouseholdId = "household-1" });
        GivenBillSplitSettlement("bs-1",
            new BillSplitMemberSettlementDto { UserId = "ariyan", TotalOwed = 100m },
            new BillSplitMemberSettlementDto { UserId = "rihan", TotalOwed = 50m });

        var result = await _sut.GetSettlementAsync("household-1", "caller-1", From, To);

        result.Members.Single(m => m.UserId == "ariyan").NetBalance.Should().Be(400m);
        result.Members.Single(m => m.UserId == "rihan").NetBalance.Should().Be(-550m);
        result.TotalBillSplitOwed.Should().Be(150m);
    }

    [Fact]
    public async Task GetSettlementAsync_NoBillSplitsInPeriod_NetBalanceEqualsMealGiveTake()
    {
        GivenMealResult(new MealMemberCostDto { UserId = "ariyan", GiveTake = 250m });
        GivenActiveBillSplits();

        var result = await _sut.GetSettlementAsync("household-1", "caller-1", From, To);

        var ariyan = result.Members.Single(m => m.UserId == "ariyan");
        ariyan.BillSplitOwed.Should().Be(0m);
        ariyan.NetBalance.Should().Be(250m);
    }

    [Fact]
    public async Task GetSettlementAsync_BillSplitOwedByMemberWithNoMealEntries_StillAppears()
    {
        GivenMealResult(new MealMemberCostDto { UserId = "ariyan", GiveTake = 100m });
        GivenActiveBillSplits(new BillSplitDto { Id = "bs-1", HouseholdId = "household-1" });
        GivenBillSplitSettlement("bs-1", new BillSplitMemberSettlementDto { UserId = "guest-payer", TotalOwed = 40m });

        var result = await _sut.GetSettlementAsync("household-1", "caller-1", From, To);

        var guest = result.Members.Single(m => m.UserId == "guest-payer");
        guest.MealGiveTake.Should().Be(0m);
        guest.NetBalance.Should().Be(-40m);
    }

    [Fact]
    public async Task GetSettlementAsync_MultipleBillSplitsInPeriod_SumsOwedAcrossAll()
    {
        GivenMealResult(new MealMemberCostDto { UserId = "ariyan", GiveTake = 0m });
        GivenActiveBillSplits(
            new BillSplitDto { Id = "bs-1", HouseholdId = "household-1" },
            new BillSplitDto { Id = "bs-2", HouseholdId = "household-1" });
        GivenBillSplitSettlement("bs-1", new BillSplitMemberSettlementDto { UserId = "ariyan", TotalOwed = 30m });
        GivenBillSplitSettlement("bs-2", new BillSplitMemberSettlementDto { UserId = "ariyan", TotalOwed = 70m });

        var result = await _sut.GetSettlementAsync("household-1", "caller-1", From, To);

        result.Members.Single(m => m.UserId == "ariyan").BillSplitOwed.Should().Be(100m);
    }
}
