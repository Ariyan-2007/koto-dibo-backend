namespace KotoDibo.Application.Features.Budget.DTOs;

public record CreateBudgetCategoryInput
{
    public string CategoryId { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public bool RolloverEnabled { get; init; }
    public string? Notes { get; init; }
}

public record CreateBudgetRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Currency { get; init; }
    public string PeriodType { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }

    // Required only when PeriodType is Custom — Weekly/Monthly/Yearly derive EndDate from
    // StartDate automatically.
    public DateOnly? EndDate { get; init; }

    public string? Notes { get; init; }
    public List<CreateBudgetCategoryInput>? Categories { get; init; }
}
