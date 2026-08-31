using FluentAssertions;
using KotoDibo.Application.Common;

namespace KotoDibo.UnitTests.Common;

public class LocalDateTests
{
    [Fact]
    public void TodayFor_LateUtcEvening_RollsForwardToNextLocalDay()
    {
        // 19:00 UTC on Aug 31 is 01:00 the next day in Bangladesh (UTC+6) — local "today" has
        // already rolled over even though the UTC calendar date has not.
        var utcNow = new DateTime(2026, 8, 31, 19, 0, 0, DateTimeKind.Utc);

        LocalDate.TodayFor(utcNow).Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void TodayFor_EarlyUtcMorning_MatchesUtcCalendarDate()
    {
        var utcNow = new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc);

        LocalDate.TodayFor(utcNow).Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void TodayFor_ExactSixHourBoundary_RollsOverAtLocalMidnight()
    {
        var utcNow = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc);

        LocalDate.TodayFor(utcNow).Should().Be(new DateOnly(2026, 9, 1));
    }
}
