using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/budgets")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly ICurrentUserService _currentUserService;

    public BudgetsController(IBudgetService budgetService, ICurrentUserService currentUserService)
    {
        _budgetService = budgetService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetSummaryDto>>> GetAll(
        [FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var budgets = await _budgetService.GetAllAsync(UserId, status, from, to, cancellationToken);
        return Ok(budgets);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BudgetDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.GetByIdAsync(UserId, id, cancellationToken);
        return Ok(budget);
    }

    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create(CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = budget.Id }, budget);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<BudgetDto>> Update(string id, UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.UpdateAsync(UserId, id, request, cancellationToken);
        return Ok(budget);
    }

    [HttpPost("{id}/categories")]
    public async Task<ActionResult<BudgetDto>> AddCategory(string id, AddBudgetCategoryRequest request, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.AddCategoryAsync(UserId, id, request, cancellationToken);
        return Ok(budget);
    }

    [HttpPost("{id}/categories/{allocationId}/adjust")]
    public async Task<ActionResult<BudgetDto>> AdjustCategory(string id, string allocationId, AdjustBudgetCategoryRequest request, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.AdjustCategoryAsync(UserId, id, allocationId, request, cancellationToken);
        return Ok(budget);
    }

    [HttpPost("{id}/categories/{allocationId}/transfer")]
    public async Task<ActionResult<BudgetDto>> TransferCategory(string id, string allocationId, TransferBudgetCategoryRequest request, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.TransferCategoryAsync(UserId, id, allocationId, request, cancellationToken);
        return Ok(budget);
    }

    [HttpGet("{id}/categories/{allocationId}/adjustments")]
    public async Task<ActionResult<IReadOnlyList<BudgetAdjustmentDto>>> GetAdjustmentHistory(string id, string allocationId, CancellationToken cancellationToken)
    {
        var history = await _budgetService.GetAdjustmentHistoryAsync(UserId, id, allocationId, cancellationToken);
        return Ok(history);
    }

    [HttpPost("{id}/rollover")]
    public async Task<ActionResult<BudgetDto>> Rollover(string id, RolloverBudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = await _budgetService.RolloverAsync(UserId, id, request, cancellationToken);
        return Ok(budget);
    }

    private string UserId => _currentUserService.UserId!;
}
