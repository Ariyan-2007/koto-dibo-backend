using KotoDibo.Application.Common;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Application.Features.RecurringExpenses.DTOs;
using KotoDibo.Application.Features.RecurringExpenses.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/recurring-expenses")]
[Authorize]
public class RecurringExpensesController : ControllerBase
{
    private readonly IRecurringExpenseService _recurringExpenseService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RecurringExpensesController(
        IRecurringExpenseService recurringExpenseService,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _recurringExpenseService = recurringExpenseService;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringExpenseDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var recurring = await _recurringExpenseService.GetAllAsync(UserId, includeInactive, cancellationToken);
        return Ok(recurring);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RecurringExpenseDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var recurring = await _recurringExpenseService.GetByIdAsync(UserId, id, cancellationToken);
        return Ok(recurring);
    }

    [HttpPost]
    public async Task<ActionResult<RecurringExpenseDto>> Create(CreateRecurringExpenseRequest request, CancellationToken cancellationToken)
    {
        var recurring = await _recurringExpenseService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = recurring.Id }, recurring);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<RecurringExpenseDto>> Update(string id, UpdateRecurringExpenseRequest request, CancellationToken cancellationToken)
    {
        var recurring = await _recurringExpenseService.UpdateAsync(UserId, id, request, cancellationToken);
        return Ok(recurring);
    }

    [HttpPost("{id}/deactivate")]
    public async Task<ActionResult<RecurringExpenseDto>> Deactivate(string id, CancellationToken cancellationToken)
    {
        var recurring = await _recurringExpenseService.DeactivateAsync(UserId, id, cancellationToken);
        return Ok(recurring);
    }

    // Manual trigger complementing the background sweep (RecurringExpenseGenerationHostedService)
    // — lets a client force "catch me up right now" instead of waiting for the next sweep interval.
    // Idempotent either way (see RecurringExpenseGenerator).
    [HttpPost("generate-due")]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> GenerateDue(CancellationToken cancellationToken)
    {
        var today = LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        var generated = await _recurringExpenseService.GenerateDueOccurrencesAsync(UserId, today, cancellationToken);
        return Ok(generated);
    }

    private string UserId => _currentUserService.UserId!;
}
