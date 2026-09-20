namespace KotoDibo.Application.Features.Withdrawals.DTOs;

public record WithdrawalDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string WithdrawnByUserId { get; init; } = string.Empty;
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
