namespace KotoDibo.Application.Features.Expenses.DTOs;

// Partial update — every field is optional and only provided ones are applied, same PATCH
// semantics as UpdateBillSplitRequest.
public record UpdateExpenseRequest
{
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? CategoryId { get; init; }
    public string? Merchant { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public DateOnly? Date { get; init; }
    public string? PaymentMethod { get; init; }
    public List<string>? Tags { get; init; }
    public string? ReceiptUrl { get; init; }
}
