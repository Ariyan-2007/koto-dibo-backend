using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Invites.DTOs;
using KotoDibo.Application.Features.Invites.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

// Household-admin side of the invite flow: create/list/revoke. The redeem side (preview + accept
// by code) is unauthenticated-by-household — see InvitesController at api/invites.
[ApiController]
[Route("api/households/{householdId}/invites")]
[Authorize]
public class HouseholdInvitesController : ControllerBase
{
    private readonly IHouseholdInviteService _inviteService;
    private readonly ICurrentUserService _currentUserService;

    public HouseholdInvitesController(IHouseholdInviteService inviteService, ICurrentUserService currentUserService)
    {
        _inviteService = inviteService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdInviteDto>> Create(string householdId, CreateHouseholdInviteRequest request, CancellationToken cancellationToken)
    {
        var invite = await _inviteService.CreateAsync(householdId, UserId, request, cancellationToken);
        return Ok(invite);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HouseholdInviteDto>>> GetPending(string householdId, CancellationToken cancellationToken)
    {
        var invites = await _inviteService.GetPendingAsync(householdId, UserId, cancellationToken);
        return Ok(invites);
    }

    [HttpPost("{inviteId}/revoke")]
    public async Task<IActionResult> Revoke(string householdId, string inviteId, CancellationToken cancellationToken)
    {
        await _inviteService.RevokeAsync(householdId, UserId, inviteId, cancellationToken);
        return NoContent();
    }

    private string UserId => _currentUserService.UserId!;
}
