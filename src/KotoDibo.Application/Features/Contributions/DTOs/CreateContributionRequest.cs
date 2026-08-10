namespace KotoDibo.Application.Features.Contributions.DTOs;

public record CreateContributionRequest
{
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
