namespace KotoDibo.Application.Features.Bazar.DTOs;

public record UpdateBazarPurchaseRequest
{
    public DateOnly? Date { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Note { get; init; }

    // "Personal" or "HouseholdFund" — omit to leave the purchase's current funding source
    // unchanged. Switching sources reconciles the mirrored Contribution (see BazarPurchaseService).
    public string? FundingSource { get; init; }
}
