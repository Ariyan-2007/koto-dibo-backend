namespace KotoDibo.Application.Features.Withdrawals.DTOs;

public record CreateWithdrawalRequest
{
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
