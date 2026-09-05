using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.DTOs;
using KotoDibo.Application.Features.ExpenseCategories.Services;
using KotoDibo.Application.Features.ExpenseCategories.Validators;
using KotoDibo.Domain.Entities;
using Moq;

namespace KotoDibo.UnitTests.Features.ExpenseCategories;

public class ExpenseCategoryServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<ExpenseCategory>> _categories = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly ExpenseCategoryService _sut;

    public ExpenseCategoryServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _categories.Setup(x => x.AddAsync(It.IsAny<ExpenseCategory>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseCategory, CancellationToken>((c, _) => c.Id = "category-1")
            .ReturnsAsync((ExpenseCategory c, CancellationToken _) => c);

        _sut = new ExpenseCategoryService(
            _categories.Object,
            _dateTimeProvider.Object,
            new CreateExpenseCategoryRequestValidator(),
            new UpdateExpenseCategoryRequestValidator());
    }

    [Fact]
    public async Task CreateAsync_persists_user_owned_category()
    {
        var result = await _sut.CreateAsync("user-1", new CreateExpenseCategoryRequest { Name = "Side Hustle" }, CancellationToken.None);

        result.Name.Should().Be("Side Hustle");
        result.IsSystemDefault.Should().BeFalse();
        _categories.Verify(x => x.AddAsync(It.Is<ExpenseCategory>(c => c.UserId == "user-1" && !c.IsSystemDefault), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_name_at_same_level()
    {
        _categories.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<ExpenseCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "existing", UserId = "user-1", Name = "Side Hustle" });

        var act = () => _sut.CreateAsync("user-1", new CreateExpenseCategoryRequest { Name = "Side Hustle" }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_subcategory_of_a_subcategory()
    {
        _categories.Setup(x => x.GetByIdAsync("child-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "child-1", UserId = null, ParentCategoryId = "parent-1", Name = "Groceries", IsActive = true });

        var act = () => _sut.CreateAsync("user-1", new CreateExpenseCategoryRequest { Name = "Organic Groceries", ParentCategoryId = "child-1" }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _categories.Verify(x => x.AddAsync(It.IsAny<ExpenseCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequireVisibleAsync_allows_system_default_category()
    {
        _categories.Setup(x => x.GetByIdAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "sys-1", UserId = null, Name = "Food", IsSystemDefault = true, IsActive = true });

        var category = await _sut.RequireVisibleAsync("user-1", "sys-1", CancellationToken.None);

        category.Name.Should().Be("Food");
    }

    [Fact]
    public async Task RequireVisibleAsync_rejects_another_users_private_category()
    {
        _categories.Setup(x => x.GetByIdAsync("private-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "private-1", UserId = "user-2", Name = "Their category", IsActive = true });

        var act = () => _sut.RequireVisibleAsync("user-1", "private-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RequireVisibleAsync_rejects_inactive_category()
    {
        _categories.Setup(x => x.GetByIdAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "sys-1", UserId = null, Name = "Food", IsActive = false });

        var act = () => _sut.RequireVisibleAsync("user-1", "sys-1", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeactivateAsync_throws_when_category_belongs_to_another_user()
    {
        _categories.Setup(x => x.GetByIdAsync("mine", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory { Id = "mine", UserId = "user-2", Name = "Theirs" });

        var act = () => _sut.DeactivateAsync("user-1", "mine", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAllAsync_includes_system_defaults_and_own_categories_only()
    {
        _categories.Setup(x => x.FindAsync(It.IsAny<Expression<Func<ExpenseCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExpenseCategory { Id = "sys", UserId = null, Name = "Food", IsActive = true },
                new ExpenseCategory { Id = "mine", UserId = "user-1", Name = "Side Hustle", IsActive = true },
            ]);

        var result = await _sut.GetAllAsync("user-1", includeInactive: false, CancellationToken.None);

        result.Should().HaveCount(2);
    }
}
