namespace KotoDibo.Application.Features.ExpenseCategories.DTOs;

public record CreateExpenseCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ParentCategoryId { get; init; }
    public string? Icon { get; init; }
}
