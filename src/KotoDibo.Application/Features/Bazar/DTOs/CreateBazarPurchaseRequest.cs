using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Bazar.DTOs;

public record CreateBazarPurchaseRequest
{
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Note { get; init; }

    // "Personal" (default, out-of-pocket — mirrored as a Contribution) or "HouseholdFund" (paid
    // from the shared balance instead). Omitting the field keeps the pre-existing behavior.
    public string FundingSource { get; init; } = nameof(BazarFundingSource.Personal);
}
