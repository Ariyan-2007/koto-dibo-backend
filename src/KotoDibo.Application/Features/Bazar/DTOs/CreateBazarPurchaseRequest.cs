namespace KotoDibo.Application.Features.Bazar.DTOs;

public record CreateBazarPurchaseRequest
{
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Note { get; init; }
}
