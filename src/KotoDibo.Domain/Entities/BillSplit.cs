namespace KotoDibo.Domain.Entities;

public class BillSplit
{
    public string Id { get; set; } = string.Empty;
    public string? HouseholdId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}
