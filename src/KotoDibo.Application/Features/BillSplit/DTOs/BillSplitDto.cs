namespace KotoDibo.Application.Features.BillSplit.DTOs;

public record BillSplitMemberInputDto
{
    public string UserId { get; init; } = string.Empty;
    public decimal Value { get; init; }
}

public record BillSplitDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string CreatedByUserId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string SplitMethod { get; init; } = string.Empty;
    public DateOnly PeriodFrom { get; init; }
    public DateOnly PeriodTo { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? TariffCountry { get; init; }
    public string? TariffProvider { get; init; }
    public decimal? MainMeterUsage { get; init; }
    public decimal? TotalAmount { get; init; }
    public IReadOnlyList<BillSplitMemberInputDto> MemberInputs { get; init; } = [];
    public string? Notes { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
