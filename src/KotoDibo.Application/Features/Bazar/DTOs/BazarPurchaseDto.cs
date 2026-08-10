namespace KotoDibo.Application.Features.Bazar.DTOs;

public record BazarPurchaseDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string PurchasedByUserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
