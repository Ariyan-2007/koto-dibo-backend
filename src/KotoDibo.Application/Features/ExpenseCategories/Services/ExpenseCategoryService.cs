using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.DTOs;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Features.ExpenseCategories.Services;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IRepository<ExpenseCategory> _categories;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateExpenseCategoryRequest> _createValidator;
    private readonly IValidator<UpdateExpenseCategoryRequest> _updateValidator;

    public ExpenseCategoryService(
        IRepository<ExpenseCategory> categories,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateExpenseCategoryRequest> createValidator,
        IValidator<UpdateExpenseCategoryRequest> updateValidator)
    {
        _categories = categories;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ExpenseCategoryDto> CreateAsync(string userId, CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.ParentCategoryId is not null)
        {
            var parent = await RequireVisibleAsync(userId, request.ParentCategoryId, cancellationToken);

            // ExpenseCategory only models one level of subcategory nesting (see its own doc
            // comment) — nothing else in the system (budget allocations, dashboards) expects or
            // renders a deeper chain, so a subcategory can't itself become a parent.
            if (parent.ParentCategoryId is not null)
            {
                throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.ParentCategoryId)] = ["A subcategory cannot itself be used as a parent category (only one level of nesting is supported)."],
                });
            }
        }

        var name = request.Name.Trim();
        var duplicate = await _categories.FindOneAsync(
            c => c.UserId == userId && c.ParentCategoryId == request.ParentCategoryId && c.IsActive && c.Name == name,
            cancellationToken);
        if (duplicate is not null)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = [$"A category named '{name}' already exists at this level."],
            });
        }

        var now = _dateTimeProvider.UtcNow;
        var category = new ExpenseCategory
        {
            UserId = userId,
            ParentCategoryId = request.ParentCategoryId,
            Name = name,
            Icon = request.Icon?.Trim(),
            IsSystemDefault = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _categories.AddAsync(category, cancellationToken);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> GetAllAsync(string userId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var categories = await _categories.FindAsync(
            c => c.UserId == null || c.UserId == userId,
            cancellationToken);

        return categories
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.ParentCategoryId != null)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<ExpenseCategoryDto> UpdateAsync(string userId, string id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var category = await GetOwnedAsync(userId, id, cancellationToken);

        if (request.Name is not null)
        {
            category.Name = request.Name.Trim();
        }

        if (request.Icon is not null)
        {
            category.Icon = request.Icon.Trim();
        }

        if (request.IsActive is not null)
        {
            category.IsActive = request.IsActive.Value;
        }

        category.UpdatedAt = _dateTimeProvider.UtcNow;
        await _categories.UpdateAsync(category, cancellationToken);
        return ToDto(category);
    }

    public async Task DeactivateAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedAsync(userId, id, cancellationToken);
        category.IsActive = false;
        category.UpdatedAt = _dateTimeProvider.UtcNow;
        await _categories.UpdateAsync(category, cancellationToken);
    }

    // A category is "visible" to a user if it's a shared system default or one they created —
    // used when validating a ParentCategoryId/CategoryId reference on another entity (Expense,
    // Budget category allocation) without exposing another user's private categories.
    public async Task<ExpenseCategory> RequireVisibleAsync(string userId, string categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || (category.UserId is not null && category.UserId != userId) || !category.IsActive)
        {
            throw new NotFoundException("ExpenseCategory", categoryId);
        }

        return category;
    }

    private async Task<ExpenseCategory> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken);
        if (category is null || category.UserId != userId)
        {
            throw new NotFoundException("ExpenseCategory", id);
        }

        return category;
    }

    private static ExpenseCategoryDto ToDto(ExpenseCategory category) => new()
    {
        Id = category.Id,
        ParentCategoryId = category.ParentCategoryId,
        Name = category.Name,
        Icon = category.Icon,
        IsSystemDefault = category.IsSystemDefault,
        IsActive = category.IsActive,
    };
}
