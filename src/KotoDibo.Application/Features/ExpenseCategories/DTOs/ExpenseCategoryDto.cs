namespace KotoDibo.Application.Features.ExpenseCategories.DTOs;

public record ExpenseCategoryDto
{
    public string Id { get; init; } = string.Empty;
    public string? ParentCategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public bool IsSystemDefault { get; init; }
    public bool IsActive { get; init; }
}
