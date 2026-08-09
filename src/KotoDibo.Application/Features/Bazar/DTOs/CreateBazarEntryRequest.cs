namespace KotoDibo.Application.Features.Bazar.DTOs;

public record CreateBazarEntryRequest
{
    public string UserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; } = default;
    public decimal Amount { get; init; } = default;
    public string Description { get; init; } = string.Empty;
}
