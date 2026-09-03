using FluentAssertions;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.UnitTests.Domain.Calculations;

public class RecurringExpenseGeneratorTests
{
    private static RecurringExpense Monthly(DateOnly start, DateOnly? end = null, DateOnly? lastGenerated = null) => new()
    {
        Id = "recurring-1",
        UserId = "user-1",
        Frequency = RecurrenceFrequency.Monthly,
        StartDate = start,
        EndDate = end,
        LastGeneratedDate = lastGenerated,
        IsActive = true,
    };

    [Theory]
    [InlineData(RecurrenceFrequency.Daily, "2026-01-01", "2026-01-02")]
    [InlineData(RecurrenceFrequency.Weekly, "2026-01-01", "2026-01-08")]
    [InlineData(RecurrenceFrequency.Biweekly, "2026-01-01", "2026-01-15")]
    [InlineData(RecurrenceFrequency.Monthly, "2026-01-31", "2026-02-28")]
    [InlineData(RecurrenceFrequency.Quarterly, "2026-01-01", "2026-04-01")]
    [InlineData(RecurrenceFrequency.Yearly, "2026-01-01", "2027-01-01")]
    public void ComputeNextOccurrence_steps_by_frequency(RecurrenceFrequency frequency, string date, string expected)
    {
        var result = RecurringExpenseGenerator.ComputeNextOccurrence(DateOnly.Parse(date), frequency);

        result.Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void GetDueOccurrences_first_occurrence_is_start_date()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1));

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 1, 1));

        due.Should().Equal(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void GetDueOccurrences_returns_nothing_before_start_date()
    {
        var recurring = Monthly(new DateOnly(2026, 3, 1));

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 1, 1));

        due.Should().BeEmpty();
    }

    [Fact]
    public void GetDueOccurrences_returns_multiple_missed_occurrences_in_one_catch_up_run()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1));

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 4, 1));

        due.Should().Equal(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1));
    }

    [Fact]
    public void GetDueOccurrences_is_idempotent_once_last_generated_reaches_asof_date()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1), lastGenerated: new DateOnly(2026, 3, 1));

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 3, 1));

        due.Should().BeEmpty("everything up to and including this date was already generated");
    }

    [Fact]
    public void GetDueOccurrences_resumes_from_last_generated_not_from_start_date()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1), lastGenerated: new DateOnly(2026, 3, 1));

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 5, 1));

        due.Should().Equal(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1));
    }

    [Fact]
    public void GetDueOccurrences_bounded_by_end_date()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1), end: new DateOnly(2026, 3, 15));

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 6, 1));

        due.Should().Equal(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1));
    }

    [Fact]
    public void GetDueOccurrences_returns_nothing_when_inactive()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1));
        recurring.IsActive = false;

        var due = RecurringExpenseGenerator.GetDueOccurrences(recurring, new DateOnly(2026, 6, 1));

        due.Should().BeEmpty();
    }

    [Fact]
    public void PeekNextOccurrence_without_prior_generation_is_start_date()
    {
        var recurring = Monthly(new DateOnly(2026, 5, 10));

        RecurringExpenseGenerator.PeekNextOccurrence(recurring).Should().Be(new DateOnly(2026, 5, 10));
    }

    [Fact]
    public void PeekNextOccurrence_after_generation_steps_from_last_generated()
    {
        var recurring = Monthly(new DateOnly(2026, 1, 1), lastGenerated: new DateOnly(2026, 3, 1));

        RecurringExpenseGenerator.PeekNextOccurrence(recurring).Should().Be(new DateOnly(2026, 4, 1));
    }
}
