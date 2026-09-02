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

    private string UserId => _currentUserService.UserId!;
}
