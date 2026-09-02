using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Invites.DTOs;
using KotoDibo.Application.Features.Invites.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

// Redeem side of the invite flow, reached either by scanning the invite QR (which encodes the
// frontend deep link containing the code) or by typing the code in by hand. Authorize-gated like
// every other endpoint in this API — a logged-out scanner is routed through login/register by the
// frontend first, then this resolves the code once a JWT exists.
[ApiController]
[Route("api/invites")]
[Authorize]
public class InvitesController : ControllerBase
{
    private readonly IHouseholdInviteService _inviteService;
    private readonly ICurrentUserService _currentUserService;

    public InvitesController(IHouseholdInviteService inviteService, ICurrentUserService currentUserService)
    {
        _inviteService = inviteService;
        _currentUserService = currentUserService;
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<InvitePreviewDto>> Preview(string code, CancellationToken cancellationToken)
    {
        var preview = await _inviteService.PreviewAsync(code, UserId, cancellationToken);
        return Ok(preview);
    }

    [HttpPost("{code}/accept")]
    public async Task<ActionResult<AcceptInviteResultDto>> Accept(string code, CancellationToken cancellationToken)
    {
        var result = await _inviteService.AcceptAsync(code, UserId, cancellationToken);
        return Ok(result);
    }

    private string UserId => _currentUserService.UserId!;
}
