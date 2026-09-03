using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.BudgetDashboard.DTOs;

public record DashboardQuery
{
    // An explicit From/To always wins over Preset (see DateRangeResolver) — Preset is only a
    // convenience for clients that don't want to compute calendar boundaries themselves.
    public DashboardPeriodPreset? Preset { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    // Pins the "budget"/"categoryBreakdown" sections to one specific Budget instead of whichever
    // one the resolved period happens to overlap.
    public string? BudgetId { get; init; }

    public string? Currency { get; init; }
    public DashboardComparisonPeriod ComparisonPeriod { get; init; } = DashboardComparisonPeriod.PreviousPeriod;
}
