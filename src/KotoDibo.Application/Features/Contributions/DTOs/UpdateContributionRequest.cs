namespace KotoDibo.Application.Features.Contributions.DTOs;

public record UpdateContributionRequest
{
    public DateOnly? Date { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Notes { get; init; }
}
