namespace KotoDibo.Application.Features.Budget.DTOs;

public record UpdateBudgetRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }

    // Status transitions are validated against the allowed lifecycle graph (see BudgetService) —
    // this isn't a free-form field overwrite.
    public string? Status { get; init; }
}
