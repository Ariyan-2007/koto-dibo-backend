using FluentAssertions;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Entities;

namespace KotoDibo.UnitTests.Domain.Calculations;

public class FairSplitAllocatorTests
{
    private static List<TariffBand> BangladeshLikeBands() =>
    [
        new() { FromUnits = 0, ToUnits = 100, RatePerUnit = 5m },
        new() { FromUnits = 100, ToUnits = 400, RatePerUnit = 7m },
        new() { FromUnits = 400, ToUnits = null, RatePerUnit = 10m },
    ];

    [Fact]
    public void ComputeTariffMetered_WorkedExample_BandWalkAndAttributionMatchExactly()
    {
        var bands = BangladeshLikeBands();
        var memberUsage = new Dictionary<string, decimal> { ["ariyan"] = 350m, ["rihan"] = 100m };
        var activeMemberIds = new[] { "ariyan", "rihan", "tanvir" };

        var result = FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: 500m, memberUsage, activeMemberIds, fixedCharges: []);

        // Band walk over total usage: 100 units @5, 300 units @7, 100 units @10.
        result.Bands.Should().HaveCount(3);
        result.Bands[0].UnitsInBand.Should().Be(100m);
        result.Bands[0].Cost.Should().Be(500m);
        result.Bands[1].UnitsInBand.Should().Be(300m);
        result.Bands[1].Cost.Should().Be(2100m);
        result.Bands[2].UnitsInBand.Should().Be(100m);
        result.Bands[2].Cost.Should().Be(1000m);
        result.TotalAmount.Should().Be(3600m);

        // Expensive bands (rate 10, then 7) are attributed to the 450 metered units first; only the
        // last 50 units of the cheapest band are left over as shared/common-area usage.
        result.Bands[2].AttributedUnits.Should().Be(100m);
        result.Bands[1].AttributedUnits.Should().Be(300m);
        result.Bands[0].AttributedUnits.Should().Be(50m);
        result.Bands[0].SharedUnits.Should().Be(50m);
        result.AttributedCost.Should().Be(3350m);
        result.SharedCost.Should().Be(250m);

        var ariyan = result.Members.Single(m => m.UserId == "ariyan");
        var rihan = result.Members.Single(m => m.UserId == "rihan");
        var tanvir = result.Members.Single(m => m.UserId == "tanvir");

        ariyan.AttributedCost.Should().Be(2605.56m);
        rihan.AttributedCost.Should().Be(744.44m);
        tanvir.AttributedCost.Should().Be(0m);

        ariyan.SharedCost.Should().Be(83.34m);
        rihan.SharedCost.Should().Be(83.33m);
        tanvir.SharedCost.Should().Be(83.33m);

        ariyan.TotalOwed.Should().Be(2688.90m);
        rihan.TotalOwed.Should().Be(827.77m);
        tanvir.TotalOwed.Should().Be(83.33m);

        result.Members.Sum(m => m.TotalOwed).Should().Be(result.TotalAmount);
    }

    [Fact]
    public void ComputeTariffMetered_UsageEntirelyAttributed_NoSharedCost()
    {
        var bands = BangladeshLikeBands();
        var memberUsage = new Dictionary<string, decimal> { ["a"] = 100m, ["b"] = 400m };

        var result = FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: 500m, memberUsage, activeMemberIds: ["a", "b"], fixedCharges: []);

        result.SharedCost.Should().Be(0m);
        result.AttributedCost.Should().Be(result.TotalAmount);
        result.Members.Sum(m => m.TotalOwed).Should().Be(result.TotalAmount);
    }

    [Fact]
    public void ComputeTariffMetered_NoSubMeteredUsage_EntireCostIsShared()
    {
        var bands = BangladeshLikeBands();
        var memberUsage = new Dictionary<string, decimal>();

        var result = FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: 500m, memberUsage, activeMemberIds: ["a", "b"], fixedCharges: []);

        result.AttributedCost.Should().Be(0m);
        result.SharedCost.Should().Be(result.TotalAmount);
        result.Members.Sum(m => m.TotalOwed).Should().Be(result.TotalAmount);
    }

    [Fact]
    public void ComputeTariffMetered_MemberUsageExceedsTotal_Throws()
    {
        var bands = BangladeshLikeBands();
        var memberUsage = new Dictionary<string, decimal> { ["a"] = 600m };

        var act = () => FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: 500m, memberUsage, activeMemberIds: ["a"], fixedCharges: []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComputeTariffMetered_NegativeUsage_Throws()
    {
        var bands = BangladeshLikeBands();

        var act = () => FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: -1m, new Dictionary<string, decimal>(), activeMemberIds: [], fixedCharges: []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComputeTariffMetered_NoBands_Throws()
    {
        var act = () => FairSplitAllocator.ComputeTariffMetered([], totalUsage: 100m, new Dictionary<string, decimal>(), activeMemberIds: [], fixedCharges: []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComputeTariffMetered_WithFixedCharges_SplitEquallyAcrossActiveMembersRegardlessOfUsage()
    {
        var bands = BangladeshLikeBands();
        var memberUsage = new Dictionary<string, decimal> { ["ariyan"] = 350m, ["rihan"] = 100m };
        var activeMemberIds = new[] { "ariyan", "rihan", "tanvir" };
        var fixedCharges = new List<BillSplitFixedCharge>
        {
            new() { Label = "Demand Charge", Amount = 300m },
            new() { Label = "VAT", Amount = 150m },
        };

        var result = FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: 500m, memberUsage, activeMemberIds, fixedCharges);

        // Fixed charges (450 total) split equally across all 3 active members — 150 each, exactly,
        // regardless of who used how much kWh (tanvir has zero metered usage but still owes 150).
        result.FixedChargesTotal.Should().Be(450m);
        result.TotalAmount.Should().Be(4050m); // 3350 attributed + 250 shared + 450 fixed

        var ariyan = result.Members.Single(m => m.UserId == "ariyan");
        var rihan = result.Members.Single(m => m.UserId == "rihan");
        var tanvir = result.Members.Single(m => m.UserId == "tanvir");

        ariyan.FixedChargeShare.Should().Be(150m);
        rihan.FixedChargeShare.Should().Be(150m);
        tanvir.FixedChargeShare.Should().Be(150m);

        ariyan.TotalOwed.Should().Be(2838.90m);
        rihan.TotalOwed.Should().Be(977.77m);
        tanvir.TotalOwed.Should().Be(233.33m);

        result.Members.Sum(m => m.TotalOwed).Should().Be(result.TotalAmount);
    }

    [Fact]
    public void ComputeTariffMetered_NoFixedCharges_FixedChargeShareIsZero()
    {
        var bands = BangladeshLikeBands();
        var memberUsage = new Dictionary<string, decimal> { ["a"] = 100m };

        var result = FairSplitAllocator.ComputeTariffMetered(bands, totalUsage: 100m, memberUsage, activeMemberIds: ["a"], fixedCharges: []);

        result.FixedChargesTotal.Should().Be(0m);
        result.Members.Should().OnlyContain(m => m.FixedChargeShare == 0m);
    }

    [Fact]
    public void ComputeFlatSplit_EqualWeights_LargestRemainderSumsExactly()
    {
        var weights = new Dictionary<string, decimal> { ["a"] = 1m, ["b"] = 1m, ["c"] = 1m };

        var result = FairSplitAllocator.ComputeFlatSplit(100m, weights);

        result.Members.Sum(m => m.TotalOwed).Should().Be(100m);
        result.Members.Should().OnlyContain(m => m.TotalOwed == 33.33m || m.TotalOwed == 33.34m);
        result.Members.Should().OnlyContain(m => m.Usage == null && m.SharedCost == 0m);
    }

    [Fact]
    public void ComputeFlatSplit_UnevenWeights_MatchesProportionalShare()
    {
        var weights = new Dictionary<string, decimal> { ["a"] = 3m, ["b"] = 1m };

        var result = FairSplitAllocator.ComputeFlatSplit(400m, weights);

        result.Members.Single(m => m.UserId == "a").TotalOwed.Should().Be(300m);
        result.Members.Single(m => m.UserId == "b").TotalOwed.Should().Be(100m);
    }
}
