using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Common;

// Resolves the dashboard's flexible period inputs (a named preset, or an explicit from/to) into
// one concrete, inclusive [From, To] range — and derives the comparison range for period-over-
// period dashboard sections. Centralized so "This Month" / "Last Month" / week boundaries are
// computed exactly once rather than re-derived slightly differently per call site.
public static class DateRangeResolver
{
    public static (DateOnly From, DateOnly To) Resolve(DashboardPeriodPreset? preset, DateOnly? from, DateOnly? to, DateOnly today)
    {
        if (from is not null || to is not null)
        {
            var resolvedFrom = from ?? to!.Value;
            var resolvedTo = to ?? from!.Value;
            return resolvedFrom <= resolvedTo ? (resolvedFrom, resolvedTo) : (resolvedTo, resolvedFrom);
        }

        return (preset ?? DashboardPeriodPreset.ThisMonth) switch
        {
            DashboardPeriodPreset.Today => (today, today),
            DashboardPeriodPreset.ThisWeek => (StartOfWeek(today), StartOfWeek(today).AddDays(6)),
            DashboardPeriodPreset.LastMonth => LastMonthRange(today),
            DashboardPeriodPreset.ThisYear => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            _ => (StartOfMonth(today), EndOfMonth(today)),
        };
    }

    public static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly EndOfMonth(DateOnly date) => StartOfMonth(date).AddMonths(1).AddDays(-1);

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    // The immediately preceding range of equal length — e.g. Feb 1-28 -> Jan 1-28 (not Jan 1-31),
    // so a partial "month to date" comparison stays apples-to-apples in span length.
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var lengthInDays = to.DayNumber - from.DayNumber + 1;
        var previousTo = from.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(lengthInDays - 1));
        return (previousFrom, previousTo);
    }

    public static (DateOnly From, DateOnly To) SamePeriodLastYear(DateOnly from, DateOnly to)
        => (from.AddYears(-1), to.AddYears(-1));

    private static (DateOnly, DateOnly) LastMonthRange(DateOnly today)
    {
        var lastMonth = StartOfMonth(today).AddMonths(-1);
        return (lastMonth, EndOfMonth(lastMonth));
    }
}
