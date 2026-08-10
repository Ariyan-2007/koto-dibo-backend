namespace KotoDibo.Domain.Calculations;

// Splits a total cost across weighted buckets (household members) so the result always sums back
// to exactly `totalCost`, even though independent per-bucket rounding to 2dp would otherwise drift
// by a few cents. This is the largest-remainder method: round every bucket down/nearest first,
// then hand out the leftover cents one at a time to the buckets with the largest fractional
// remainder (ties broken by key, for determinism) until the drift is gone.
public static class MealCostAllocator
{
    private const int CurrencyDecimals = 2;

    public static IReadOnlyDictionary<string, decimal> Allocate(decimal totalCost, IReadOnlyDictionary<string, decimal> weightsByKey)
    {
        if (weightsByKey.Count == 0)
        {
            return new Dictionary<string, decimal>();
        }

        var totalWeight = weightsByKey.Values.Sum();
        if (totalWeight <= 0)
        {
            throw new ArgumentException("Total weight must be greater than zero.", nameof(weightsByKey));
        }

        var raw = weightsByKey.ToDictionary(kv => kv.Key, kv => totalCost * kv.Value / totalWeight);
        var rounded = raw.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, CurrencyDecimals, MidpointRounding.ToEven));

        var centUnit = 1m / (decimal)Math.Pow(10, CurrencyDecimals);
        var drift = totalCost - rounded.Values.Sum();
        var driftCents = (int)Math.Round(drift / centUnit, MidpointRounding.AwayFromZero);

        if (driftCents == 0)
        {
            return rounded;
        }

        var orderedKeys = raw.Keys
            .OrderByDescending(key => raw[key] - Math.Floor(raw[key] / centUnit) * centUnit)
            .ThenBy(key => key, StringComparer.Ordinal)
            .ToList();

        var step = driftCents > 0 ? centUnit : -centUnit;
        var remaining = Math.Abs(driftCents);
        var index = 0;
        while (remaining > 0)
        {
            var key = orderedKeys[index % orderedKeys.Count];
            rounded[key] += step;
            index++;
            remaining--;
        }

        return rounded;
    }
}
