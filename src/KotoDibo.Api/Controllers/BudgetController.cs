using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Application.Features.Budget.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly ICurrentUserService _currentUserService;

    public BudgetController(IBudgetService budgetService, ICurrentUserService currentUserService)
    {
        _budgetService = budgetService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetDto>>> GetAll(CancellationToken cancellationToken)
    {
        var budgets = await _budgetService.GetAllAsync(UserId, cancellationToken);
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

    private string UserId => _currentUserService.UserId!;
}
