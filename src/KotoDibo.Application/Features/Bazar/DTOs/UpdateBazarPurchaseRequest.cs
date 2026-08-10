namespace KotoDibo.Application.Features.Bazar.DTOs;

public record UpdateBazarPurchaseRequest
{
    public DateOnly? Date { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Note { get; init; }
}
