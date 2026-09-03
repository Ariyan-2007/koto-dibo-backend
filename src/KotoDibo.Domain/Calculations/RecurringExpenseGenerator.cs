using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Calculations;

// Pure stepping/occurrence logic for RecurringExpense. Kept dependency-free (no repository, no
// clock) so RecurringExpenseService owns all the I/O and this stays trivially unit-testable.
public static class RecurringExpenseGenerator
{
    // Defensive cap: a runaway daily recurrence with a very old StartDate and no prior generation
    // run should never turn one call into an unbounded insert storm.
    private const int MaxOccurrencesPerRun = 10_000;

    public static DateOnly ComputeNextOccurrence(DateOnly date, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => date.AddDays(1),
        RecurrenceFrequency.Weekly => date.AddDays(7),
        RecurrenceFrequency.Biweekly => date.AddDays(14),
        RecurrenceFrequency.Monthly => date.AddMonths(1),
        RecurrenceFrequency.Quarterly => date.AddMonths(3),
        RecurrenceFrequency.Yearly => date.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported recurrence frequency."),
    };

    // The next date generation is due, independent of whether that date has actually been
    // reached/generated yet — used to answer "when is this due next" for upcoming-expense views.
    public static DateOnly PeekNextOccurrence(RecurringExpense recurring)
        => recurring.LastGeneratedDate is { } last
            ? ComputeNextOccurrence(last, recurring.Frequency)
            : recurring.StartDate;

    // Every occurrence date after the last one generated (or StartDate, if none yet), up to and
    // including asOfDate and bounded by EndDate. Calling this again with the same or an earlier
    // asOfDate after those occurrences were recorded (via the caller advancing LastGeneratedDate)
    // returns an empty list — that idempotency is what lets generation be triggered as often as
    // the caller likes (a background sweep, a manual "generate now", a dashboard refresh) without
    // ever double-materializing an expense.
    public static IReadOnlyList<DateOnly> GetDueOccurrences(RecurringExpense recurring, DateOnly asOfDate)
    {
        if (!recurring.IsActive)
        {
            return [];
        }

        var effectiveEnd = recurring.EndDate is { } end && end < asOfDate ? end : asOfDate;

        var occurrences = new List<DateOnly>();
        var cursor = PeekNextOccurrence(recurring);

        while (cursor <= effectiveEnd && occurrences.Count < MaxOccurrencesPerRun)
        {
            occurrences.Add(cursor);
            cursor = ComputeNextOccurrence(cursor, recurring.Frequency);
        }

        return occurrences;
    }
}
