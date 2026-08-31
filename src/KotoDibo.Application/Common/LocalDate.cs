namespace KotoDibo.Application.Common;

// Bangladesh (UTC+6) is this MVP's only market (see MVP_BLUEPRINT.md's Bangladesh tariff seed) and
// no per-household/per-user timezone is modeled yet. "Today" for future-date checks must anchor to
// the household's local calendar day, not raw UTC — otherwise a member logging today's date gets
// rejected as "in the future" for the first ~6 hours of every local day, while UTC still reads
// yesterday. Revisit this fixed offset if/when per-household timezones are ever modeled.
public static class LocalDate
{
    private static readonly TimeSpan BangladeshOffset = TimeSpan.FromHours(6);

    public static DateOnly TodayFor(DateTime utcNow) => DateOnly.FromDateTime(utcNow + BangladeshOffset);
}
