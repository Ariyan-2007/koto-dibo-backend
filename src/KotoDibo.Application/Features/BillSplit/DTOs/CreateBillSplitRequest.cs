namespace KotoDibo.Application.Features.BillSplit.DTOs;

public record CreateBillSplitRequest
{
    public string Title { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; } = default;
    public bool IsAnonymous { get; init; } = false;
}
