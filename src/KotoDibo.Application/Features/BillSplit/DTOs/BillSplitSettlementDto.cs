namespace KotoDibo.Application.Features.BillSplit.DTOs;

public record BillSplitBandDto
{
    public decimal FromUnits { get; init; }
    public decimal? ToUnits { get; init; }
    public decimal RatePerUnit { get; init; }
    public decimal UnitsInBand { get; init; }
    public decimal AttributedUnits { get; init; }
    public decimal SharedUnits { get; init; }
    public decimal Cost { get; init; }
}

public record BillSplitMemberSettlementDto
{
    public string UserId { get; init; } = string.Empty;
    public decimal? Usage { get; init; }
    public decimal AttributedCost { get; init; }
    public decimal SharedCost { get; init; }
    public decimal FixedChargeShare { get; init; }
    public decimal TotalOwed { get; init; }
}

public record BillSplitSettlementDto
{
    public string BillSplitId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal AttributedCost { get; init; }
    public decimal SharedCost { get; init; }
    public decimal FixedChargesTotal { get; init; }
    public IReadOnlyList<BillSplitBandDto> Bands { get; init; } = [];
    public IReadOnlyList<BillSplitMemberSettlementDto> Members { get; init; } = [];
    public string CalculationVersion { get; init; } = "v1";
}
