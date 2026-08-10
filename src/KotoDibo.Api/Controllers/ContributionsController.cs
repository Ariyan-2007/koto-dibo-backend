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
        var contribution = await _contributionService.CreateAsync(householdId, UserId, request, cancellationToken);
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

    [HttpPost("{contributionId}/cancel")]
    public async Task<ActionResult<ContributionDto>> Cancel(string householdId, string contributionId, CancellationToken cancellationToken)
    {
        var contribution = await _contributionService.CancelAsync(householdId, UserId, contributionId, cancellationToken);
        return Ok(contribution);
    }

    private string UserId => _currentUserService.UserId!;
}
