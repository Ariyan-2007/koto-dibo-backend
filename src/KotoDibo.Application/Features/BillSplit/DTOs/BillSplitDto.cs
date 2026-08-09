namespace KotoDibo.Application.Features.BillSplit.DTOs;

public record BillSplitDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; } = default;
    public bool IsAnonymous { get; init; } = false;
}
