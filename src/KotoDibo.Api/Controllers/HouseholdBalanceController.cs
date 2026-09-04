using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.HouseholdBalance.DTOs;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/balance")]
[Authorize]
public class HouseholdBalanceController : ControllerBase
{
    private readonly IHouseholdBalanceService _householdBalanceService;
    private readonly ICurrentUserService _currentUserService;

    public HouseholdBalanceController(IHouseholdBalanceService householdBalanceService, ICurrentUserService currentUserService)
    {
        _householdBalanceService = householdBalanceService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<HouseholdBalanceDto>> Get(string householdId, CancellationToken cancellationToken)
    {
        var balance = await _householdBalanceService.GetBalanceAsync(householdId, UserId, cancellationToken);
        return Ok(balance);
    }

    // Contribution + BazarPurchase rows merged into one chronological ledger — the "transaction
    // history" behind the balance above, so the frontend doesn't have to fetch and merge both lists
    // itself just to render a combined feed.
    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<HouseholdLedgerTransactionDto>>> GetTransactions(string householdId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var transactions = await _householdBalanceService.GetTransactionsAsync(householdId, UserId, from, to, status, cancellationToken);
        return Ok(transactions);
    }

    private string UserId => _currentUserService.UserId!;
}
