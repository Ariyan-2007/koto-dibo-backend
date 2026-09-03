namespace KotoDibo.Application.Features.Budget.DTOs;

public record BudgetCategoryDto
{
    public string Id { get; init; } = string.Empty;
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public bool RolloverEnabled { get; init; }
    public decimal RolloverAmount { get; init; }
    public decimal TotalAvailable { get; init; }
    public decimal Spent { get; init; }
    public decimal Remaining { get; init; }
    public decimal Variance { get; init; }
    public decimal? UsagePercentage { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public record BudgetDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string PeriodType { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }

    public decimal TotalPlanned { get; init; }
    public decimal TotalRollover { get; init; }
    public decimal TotalAvailable { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal TotalRemaining { get; init; }
    public decimal TotalOverspent { get; init; }
    public decimal? UtilizationPercentage { get; init; }
    public string Health { get; init; } = string.Empty;

    public IReadOnlyList<BudgetCategoryDto> Categories { get; init; } = [];

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

// Lightweight variant returned by list endpoints — omits the per-category breakdown so listing
// many periods doesn't pull every category's live spend computation into one response.
public record BudgetSummaryDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string PeriodType { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalPlanned { get; init; }
    public decimal TotalAvailable { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal TotalRemaining { get; init; }
    public decimal? UtilizationPercentage { get; init; }
    public string Health { get; init; } = string.Empty;
}
