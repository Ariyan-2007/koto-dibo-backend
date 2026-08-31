using KotoDibo.Domain.Entities;

namespace KotoDibo.Domain.Calculations;

public record TariffBandBreakdown
{
    public decimal FromUnits { get; init; }
    public decimal? ToUnits { get; init; }
    public decimal RatePerUnit { get; init; }
    public decimal UnitsInBand { get; init; }
    public decimal AttributedUnits { get; init; }
    public decimal SharedUnits { get; init; }
    public decimal Cost { get; init; }
}

public record FairSplitMemberResult
{
    public string UserId { get; init; } = string.Empty;
    public decimal? Usage { get; init; }
    public decimal AttributedCost { get; init; }
    public decimal SharedCost { get; init; }
    public decimal TotalOwed { get; init; }
}

public record FairSplitResult
{
    public decimal TotalAmount { get; init; }
    public decimal AttributedCost { get; init; }
    public decimal SharedCost { get; init; }
    public IReadOnlyList<TariffBandBreakdown> Bands { get; init; } = [];
    public IReadOnlyList<FairSplitMemberResult> Members { get; init; } = [];
}

// Splits a shared bill across household members. TariffMetered is the flagship, hard case: a
// progressive tariff means the household's TOTAL usage decides which bands get filled, so the
// members whose sub-metered usage pushed the household into the expensive upper bands must be
// attributed those expensive bands first — a simple proportional split would smear the cheap
// lifeline-rate units evenly across everyone and under-charge the heavy users. EqualSplit and
// WeightedSplit cover bills with no sub-meter at all (rent, wifi, gas cylinder) and reuse
// MealCostAllocator's largest-remainder rounding directly, so every ledger in the system rounds
// the same way.
public static class FairSplitAllocator
{
    // Walks the tariff bands against total household usage, then — because a progressive tariff
    // makes usage volume, not identity, decide the per-unit rate — attributes the most expensive
    // bands to sub-metered (member) usage first before falling back to cheaper bands for the
    // shared/common-area remainder. The shared cost is then split equally across `activeMemberIds`
    // (the default policy for common-area usage no one's sub-meter captures), independent of who
    // has a sub-meter reading at all.
    public static FairSplitResult ComputeTariffMetered(
        IReadOnlyList<TariffBand> bands,
        decimal totalUsage,
        IReadOnlyDictionary<string, decimal> memberUsage,
        IReadOnlyCollection<string> activeMemberIds)
    {
        if (bands.Count == 0)
        {
            throw new ArgumentException("At least one tariff band is required.", nameof(bands));
        }

        if (totalUsage < 0)
        {
            throw new ArgumentException("Total usage cannot be negative.", nameof(totalUsage));
        }

        var attributedUsageTotal = memberUsage.Values.Sum();
        if (attributedUsageTotal > totalUsage)
        {
            throw new ArgumentException("Sum of member usage cannot exceed total household usage.", nameof(memberUsage));
        }

        var orderedBands = bands.OrderBy(b => b.FromUnits).ToList();

        // Step 1: walk bands ascending to find how many units of TOTAL usage fall in each band.
        var walk = new List<(TariffBand Band, decimal UnitsInBand)>();
        var remainingTotal = totalUsage;
        foreach (var band in orderedBands)
        {
            if (remainingTotal <= 0)
            {
                walk.Add((band, 0m));
                continue;
            }

            var bandCapacity = band.ToUnits is { } to ? to - band.FromUnits : remainingTotal;
            var unitsInBand = Math.Min(bandCapacity, remainingTotal);
            walk.Add((band, unitsInBand));
            remainingTotal -= unitsInBand;
        }

        // Step 2: attribute the most expensive bands to member usage first; the rest is shared.
        var byRateDescending = walk.OrderByDescending(w => w.Band.RatePerUnit).ToList();
        var remainingAttributed = attributedUsageTotal;
        var attributedByBand = new Dictionary<TariffBand, decimal>();
        foreach (var (band, unitsInBand) in byRateDescending)
        {
            var attributedHere = Math.Min(unitsInBand, remainingAttributed);
            attributedByBand[band] = attributedHere;
            remainingAttributed -= attributedHere;
        }

        var bandBreakdowns = new List<TariffBandBreakdown>();
        decimal attributedCost = 0m;
        decimal sharedCost = 0m;
        foreach (var (band, unitsInBand) in walk)
        {
            var attributedUnits = attributedByBand.GetValueOrDefault(band, 0m);
            var sharedUnits = unitsInBand - attributedUnits;
            var cost = unitsInBand * band.RatePerUnit;
            attributedCost += attributedUnits * band.RatePerUnit;
            sharedCost += sharedUnits * band.RatePerUnit;

            bandBreakdowns.Add(new TariffBandBreakdown
            {
                FromUnits = band.FromUnits,
                ToUnits = band.ToUnits,
                RatePerUnit = band.RatePerUnit,
                UnitsInBand = unitsInBand,
                AttributedUnits = attributedUnits,
                SharedUnits = sharedUnits,
                Cost = cost,
            });
        }

        var attributedShareByUser = attributedUsageTotal > 0
            ? MealCostAllocator.Allocate(attributedCost, memberUsage.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value))
            : new Dictionary<string, decimal>();

        var sharedShareByUser = activeMemberIds.Count > 0
            ? MealCostAllocator.Allocate(sharedCost, activeMemberIds.ToDictionary(id => id, _ => 1m))
            : new Dictionary<string, decimal>();

        var userIds = memberUsage.Keys.Union(activeMemberIds).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var members = userIds.Select(userId =>
        {
            var attributedShare = attributedShareByUser.GetValueOrDefault(userId, 0m);
            var sharedShare = sharedShareByUser.GetValueOrDefault(userId, 0m);
            return new FairSplitMemberResult
            {
                UserId = userId,
                Usage = memberUsage.GetValueOrDefault(userId, 0m),
                AttributedCost = attributedShare,
                SharedCost = sharedShare,
                TotalOwed = attributedShare + sharedShare,
            };
        }).ToList();

        return new FairSplitResult
        {
            TotalAmount = attributedCost + sharedCost,
            AttributedCost = attributedCost,
            SharedCost = sharedCost,
            Bands = bandBreakdowns,
            Members = members,
        };
    }

    // EqualSplit (all weights 1) and WeightedSplit (caller-supplied weights) both reduce to the
    // same largest-remainder allocation MealCostAllocator already provides.
    public static FairSplitResult ComputeFlatSplit(decimal totalAmount, IReadOnlyDictionary<string, decimal> weightsByUser)
    {
        var shareByUser = MealCostAllocator.Allocate(totalAmount, weightsByUser);

        var members = shareByUser
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new FairSplitMemberResult
            {
                UserId = kv.Key,
                Usage = null,
                AttributedCost = kv.Value,
                SharedCost = 0m,
                TotalOwed = kv.Value,
            })
            .ToList();

        return new FairSplitResult
        {
            TotalAmount = totalAmount,
            AttributedCost = totalAmount,
            SharedCost = 0m,
            Bands = [],
            Members = members,
        };
    }
}
