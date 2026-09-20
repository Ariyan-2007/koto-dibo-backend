using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Withdrawals.DTOs;
using KotoDibo.Application.Features.Withdrawals.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/withdrawals")]
[Authorize]
public class WithdrawalsController : ControllerBase
{
    private readonly IWithdrawalService _withdrawalService;
    private readonly ICurrentUserService _currentUserService;

    public WithdrawalsController(IWithdrawalService withdrawalService, ICurrentUserService currentUserService)
    {
        _withdrawalService = withdrawalService;
        _currentUserService = currentUserService;
    }

    // Rejected with 409 (InsufficientFundsException) when the amount exceeds the household balance.
    [HttpPost]
    public async Task<ActionResult<WithdrawalDto>> Create(string householdId, CreateWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var withdrawal = await _withdrawalService.CreateAsync(householdId, UserId, UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { householdId, withdrawalId = withdrawal.Id }, withdrawal);
    }

    // On-behalf-of: an Owner/Manager records a withdrawal for another member; the deduction lands
    // on {userId}'s contribution, and the caller is recorded as CreatedByUserId.
    [HttpPost("{userId}")]
    public async Task<ActionResult<WithdrawalDto>> CreateFor(string householdId, string userId, CreateWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var withdrawal = await _withdrawalService.CreateAsync(householdId, UserId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { householdId, withdrawalId = withdrawal.Id }, withdrawal);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WithdrawalDto>>> GetList(string householdId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var withdrawals = await _withdrawalService.GetListAsync(householdId, UserId, from, to, cancellationToken);
        return Ok(withdrawals);
    }

    [HttpGet("{withdrawalId}")]
    public async Task<ActionResult<WithdrawalDto>> GetById(string householdId, string withdrawalId, CancellationToken cancellationToken)
    {
        var withdrawal = await _withdrawalService.GetByIdAsync(householdId, UserId, withdrawalId, cancellationToken);
        return Ok(withdrawal);
    }

    // Hard delete — permanently removes the withdrawal and returns the money to the balance.
    [HttpDelete("{withdrawalId}")]
    public async Task<IActionResult> Delete(string householdId, string withdrawalId, CancellationToken cancellationToken)
    {
        await _withdrawalService.DeleteAsync(householdId, UserId, withdrawalId, cancellationToken);
        return NoContent();
    }

    private string UserId => _currentUserService.UserId!;
}
