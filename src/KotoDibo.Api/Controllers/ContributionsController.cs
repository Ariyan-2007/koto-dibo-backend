using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Contributions.DTOs;
using KotoDibo.Application.Features.Contributions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/contributions")]
[Authorize]
public class ContributionsController : ControllerBase
{
    private readonly IContributionService _contributionService;
    private readonly ICurrentUserService _currentUserService;

    public ContributionsController(IContributionService contributionService, ICurrentUserService currentUserService)
    {
        _contributionService = contributionService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<ActionResult<ContributionDto>> Create(string householdId, CreateContributionRequest request, CancellationToken cancellationToken)
    {
        var contribution = await _contributionService.CreateAsync(householdId, UserId, UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { householdId, contributionId = contribution.Id }, contribution);
    }

    // On-behalf-of creation: an Owner/Manager can record a Contribution for another member (e.g.
    // cash handed to them in person). The financial credit belongs to {userId}; UserId (the caller)
    // is recorded separately as CreatedByUserId for audit — see ContributionService.CreateAsync.
    [HttpPost("{userId}")]
    public async Task<ActionResult<ContributionDto>> CreateFor(string householdId, string userId, CreateContributionRequest request, CancellationToken cancellationToken)
    {
        var contribution = await _contributionService.CreateAsync(householdId, UserId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { householdId, contributionId = contribution.Id }, contribution);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContributionDto>>> GetList(string householdId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var contributions = await _contributionService.GetListAsync(householdId, UserId, from, to, status, cancellationToken);
        return Ok(contributions);
    }

    [HttpGet("{contributionId}")]
    public async Task<ActionResult<ContributionDto>> GetById(string householdId, string contributionId, CancellationToken cancellationToken)
    {
        var contribution = await _contributionService.GetByIdAsync(householdId, UserId, contributionId, cancellationToken);
        return Ok(contribution);
    }

    [HttpPatch("{contributionId}")]
    public async Task<ActionResult<ContributionDto>> Update(string householdId, string contributionId, UpdateContributionRequest request, CancellationToken cancellationToken)
    {
        var contribution = await _contributionService.UpdateAsync(householdId, UserId, contributionId, request, cancellationToken);
        return Ok(contribution);
    }

    // Hard delete — permanently removes this contribution. No soft-cancel state: there's nothing
    // left in the database to undo this with. Rejected (400) for a Contribution auto-generated from
    // a Bazar purchase — delete that purchase instead (DELETE .../bazar/{purchaseId}).
    [HttpDelete("{contributionId}")]
    public async Task<IActionResult> Delete(string householdId, string contributionId, CancellationToken cancellationToken)
    {
        await _contributionService.DeleteAsync(householdId, UserId, contributionId, cancellationToken);
        return NoContent();
    }

    private string UserId => _currentUserService.UserId!;
}
