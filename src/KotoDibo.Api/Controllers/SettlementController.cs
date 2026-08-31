using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Settlement.DTOs;
using KotoDibo.Application.Features.Settlement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/settlement")]
[Authorize]
public class SettlementController : ControllerBase
{
    private readonly ISettlementService _settlementService;
    private readonly ICurrentUserService _currentUserService;

    public SettlementController(ISettlementService settlementService, ICurrentUserService currentUserService)
    {
        _settlementService = settlementService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<HouseholdSettlementDto>> Get(string householdId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var result = await _settlementService.GetSettlementAsync(householdId, UserId, from, to, cancellationToken);
        return Ok(result);
    }

    private string UserId => _currentUserService.UserId!;
}
