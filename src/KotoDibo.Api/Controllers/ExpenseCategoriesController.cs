using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.ExpenseCategories.DTOs;
using KotoDibo.Application.Features.ExpenseCategories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/expense-categories")]
[Authorize]
public class ExpenseCategoriesController : ControllerBase
{
    private readonly IExpenseCategoryService _categoryService;
    private readonly ICurrentUserService _currentUserService;

    public ExpenseCategoriesController(IExpenseCategoryService categoryService, ICurrentUserService currentUserService)
    {
        _categoryService = categoryService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategoryDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(UserId, includeInactive, cancellationToken);
        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseCategoryDto>> Create(CreateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), category);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ExpenseCategoryDto>> Update(string id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.UpdateAsync(UserId, id, request, cancellationToken);
        return Ok(category);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        await _categoryService.DeactivateAsync(UserId, id, cancellationToken);
        return NoContent();
    }

    private string UserId => _currentUserService.UserId!;
}
