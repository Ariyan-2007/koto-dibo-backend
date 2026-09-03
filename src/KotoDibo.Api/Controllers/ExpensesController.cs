using KotoDibo.Application.Common;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.Expenses.Interfaces;
using KotoDibo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ICurrentUserService _currentUserService;

    public ExpensesController(IExpenseService expenseService, ICurrentUserService currentUserService)
    {
        _expenseService = expenseService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExpenseDto>>> GetPaged(
        [FromQuery] string? categoryId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] string? merchant,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? tag,
        [FromQuery] bool? isRecurring,
        [FromQuery] string? search,
        [FromQuery] ExpenseSortField sortBy = ExpenseSortField.Date,
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ExpenseListQuery
        {
            CategoryId = categoryId,
            From = from,
            To = to,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Merchant = merchant,
            PaymentMethod = paymentMethod,
            Tag = tag,
            IsRecurring = isRecurring,
            Search = search,
            SortBy = sortBy,
            SortDescending = sortDescending,
            Page = page,
            PageSize = pageSize,
        };

        var result = await _expenseService.GetPagedAsync(UserId, query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExpenseDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.GetByIdAsync(UserId, id, cancellationToken);
        return Ok(expense);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, expense);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ExpenseDto>> Update(string id, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.UpdateAsync(UserId, id, request, cancellationToken);
        return Ok(expense);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ExpenseDto>> Delete(string id, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.DeleteAsync(UserId, id, cancellationToken);
        return Ok(expense);
    }

    private string UserId => _currentUserService.UserId!;
}
