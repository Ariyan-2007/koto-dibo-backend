namespace KotoDibo.Application.Features.BillSplit.DTOs;

// Patch-style update, same shape as UpdateBazarPurchaseRequest: only non-null fields are applied.
// SplitMethod, PeriodFrom/To, Currency and TariffCountry/Provider are immutable once created —
// changing the billing method or period means creating a new record, not mutating this one.
public record UpdateBillSplitRequest
{
    public string? Title { get; init; }
    public decimal? MainMeterUsage { get; init; }
    public decimal? TotalAmount { get; init; }
    public IReadOnlyList<BillSplitMemberInputDto>? MemberInputs { get; init; }
    public IReadOnlyList<BillSplitFixedChargeDto>? FixedCharges { get; init; }
    public string? Notes { get; init; }
}
