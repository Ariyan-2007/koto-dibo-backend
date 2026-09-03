namespace KotoDibo.Application.Features.ExpenseCategories.DTOs;

public record UpdateExpenseCategoryRequest
{
    public string? Name { get; init; }
    public string? Icon { get; init; }
    public bool? IsActive { get; init; }
}
