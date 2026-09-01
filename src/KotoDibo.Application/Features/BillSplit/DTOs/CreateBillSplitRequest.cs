namespace KotoDibo.Application.Features.BillSplit.DTOs;

public record CreateBillSplitRequest
{
    public string Title { get; init; } = string.Empty;
    public string SplitMethod { get; init; } = string.Empty;
    public DateOnly PeriodFrom { get; init; }
    public DateOnly PeriodTo { get; init; }
    public string Currency { get; init; } = string.Empty;

    // TariffMetered only.
    public string? TariffCountry { get; init; }
    public string? TariffProvider { get; init; }
    public decimal? MainMeterUsage { get; init; }

    // EqualSplit/WeightedSplit only.
    public decimal? TotalAmount { get; init; }

    // TariffMetered: sub-meter usage per member. WeightedSplit: fixed weight per member.
    public IReadOnlyList<BillSplitMemberInputDto> MemberInputs { get; init; } = [];

    // TariffMetered only: non-usage-based line items (demand charge, VAT, meter rent, ...) split
    // equally across active members. Ignored for EqualSplit/WeightedSplit.
    public IReadOnlyList<BillSplitFixedChargeDto> FixedCharges { get; init; } = [];

    public string? Notes { get; init; }
}
