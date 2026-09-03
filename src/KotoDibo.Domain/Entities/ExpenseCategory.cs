namespace KotoDibo.Domain.Entities;

// UserId is null for the seeded system defaults (Housing, Food, Transportation, ...), shared by
// every user; non-null for a category a specific user created. ParentCategoryId models one level
// of subcategory nesting (Food -> Groceries), matching the depth the seed data and the prompt's
// examples actually use — deeper trees would need recursive query support nothing here needs yet.
public class ExpenseCategory
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsSystemDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
