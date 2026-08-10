using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

public class Household
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Free-text label (e.g. "Bachelor House", "Family Home") rather than a fixed enum: nothing in
    // the system branches on it, so a closed set would only need widening as new household kinds
    // come up. Purely descriptive/display.
    public string? Type { get; set; }

    public HouseholdStatus Status { get; set; } = HouseholdStatus.Active;
    public string OwnerUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
