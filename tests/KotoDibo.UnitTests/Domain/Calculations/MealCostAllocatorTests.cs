using FluentAssertions;
using KotoDibo.Domain.Calculations;

namespace KotoDibo.UnitTests.Domain.Calculations;

public class MealCostAllocatorTests
{
    [Fact]
    public void Allocate_SpecWorkedExample_MatchesExactly()
    {
        var weights = new Dictionary<string, decimal>
        {
            ["ariyan"] = 35,
            ["rihan"] = 28,
            ["waythin"] = 17,
        };

        var result = MealCostAllocator.Allocate(10_000m, weights);

        result["ariyan"].Should().Be(4_375m);
        result["rihan"].Should().Be(3_500m);
        result["waythin"].Should().Be(2_125m);
        result.Values.Sum().Should().Be(10_000m);
    }

    [Fact]
    public void Allocate_ProducesRemainder_SumStillMatchesTotalExactly()
    {
        var weights = new Dictionary<string, decimal>
        {
            ["a"] = 1,
            ["b"] = 1,
            ["c"] = 1,
        };

        var result = MealCostAllocator.Allocate(1000m, weights);

        result.Values.Sum().Should().Be(1000m);
        result.Values.Should().OnlyContain(v => v == 333.33m || v == 333.34m);
    }

    [Fact]
    public void Allocate_LargeValuesAndUnevenWeights_SumMatchesTotalExactly()
    {
        var weights = new Dictionary<string, decimal>
        {
            ["a"] = 123.75m,
            ["b"] = 7.5m,
            ["c"] = 0.25m,
            ["d"] = 968.111m,
        };

        var result = MealCostAllocator.Allocate(1_234_567.89m, weights);

        result.Values.Sum().Should().Be(1_234_567.89m);
    }

    [Fact]
    public void Allocate_EmptyWeights_ReturnsEmpty()
    {
        var result = MealCostAllocator.Allocate(500m, new Dictionary<string, decimal>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Allocate_ZeroTotalCost_AllBucketsZeroButNoException()
    {
        var weights = new Dictionary<string, decimal> { ["a"] = 1, ["b"] = 1 };

        var result = MealCostAllocator.Allocate(0m, weights);

        result.Values.Sum().Should().Be(0m);
    }

    [Fact]
    public void Allocate_ZeroTotalWeight_Throws()
    {
        var weights = new Dictionary<string, decimal> { ["a"] = 0 };

        var act = () => MealCostAllocator.Allocate(100m, weights);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Allocate_SingleBucket_GetsEntireCost()
    {
        var weights = new Dictionary<string, decimal> { ["solo"] = 2.5m };

        var result = MealCostAllocator.Allocate(999.99m, weights);

        result["solo"].Should().Be(999.99m);
    }
}
