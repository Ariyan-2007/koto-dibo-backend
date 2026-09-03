using KotoDibo.Application.Features.ExpenseCategories.DTOs;
using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Features.ExpenseCategories.Interfaces;

public interface IExpenseCategoryService
{
    Task<ExpenseCategoryDto> CreateAsync(string userId, CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default);

    // System defaults (UserId == null) plus the caller's own categories, flattened — the client
    // reconstructs the parent/child tree client-side from ParentCategoryId.
    Task<IReadOnlyList<ExpenseCategoryDto>> GetAllAsync(string userId, bool includeInactive, CancellationToken cancellationToken = default);

    Task<ExpenseCategoryDto> UpdateAsync(string userId, string id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default);

    // Deactivate only — a category is never hard-deleted so historical expenses that reference its
    // Id keep resolving (they also carry a CategoryName snapshot, so display never breaks even if
    // resolution somehow failed).
    Task DeactivateAsync(string userId, string id, CancellationToken cancellationToken = default);

    // A category is "visible" to a user if it's a shared system default or one they created —
    // used by other features (Expense, Budget) to validate a CategoryId reference and resolve its
    // current Name for the historical-accuracy snapshot, without exposing another user's private
    // categories. Throws NotFoundException when the category doesn't exist, isn't visible, or is
    // inactive.
    Task<ExpenseCategory> RequireVisibleAsync(string userId, string categoryId, CancellationToken cancellationToken = default);
}
