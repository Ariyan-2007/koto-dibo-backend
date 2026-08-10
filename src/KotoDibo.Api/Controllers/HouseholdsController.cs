using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Application.Features.Households.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households")]
[Authorize]
public class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _householdService;
    private readonly IHouseholdMembershipService _membershipService;
    private readonly ICurrentUserService _currentUserService;

    public HouseholdsController(
        IHouseholdService householdService,
        IHouseholdMembershipService membershipService,
        ICurrentUserService currentUserService)
    {
        _householdService = householdService;
        _membershipService = membershipService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdDto>> Create(CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
        var household = await _householdService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = household.Id }, household);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HouseholdDto>>> GetMine(CancellationToken cancellationToken)
    {
        var households = await _householdService.GetMyHouseholdsAsync(UserId, cancellationToken);
        return Ok(households);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HouseholdDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var household = await _householdService.GetByIdAsync(id, UserId, cancellationToken);
        return Ok(household);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<HouseholdDto>> Update(string id, UpdateHouseholdRequest request, CancellationToken cancellationToken)
    {
        var household = await _householdService.UpdateAsync(id, UserId, request, cancellationToken);
        return Ok(household);
    }

    [HttpPost("{id}/archive")]
    public async Task<ActionResult<HouseholdDto>> Archive(string id, CancellationToken cancellationToken)
    {
        var household = await _householdService.ArchiveAsync(id, UserId, cancellationToken);
        return Ok(household);
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<HouseholdDto>> Restore(string id, CancellationToken cancellationToken)
    {
        var household = await _householdService.RestoreAsync(id, UserId, cancellationToken);
        return Ok(household);
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<IReadOnlyList<HouseholdMemberDto>>> GetMembers(string id, CancellationToken cancellationToken)
    {
        var members = await _membershipService.GetMembersAsync(id, UserId, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{id}/members")]
    public async Task<ActionResult<HouseholdMemberDto>> AddMember(string id, AddMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await _membershipService.AddMemberAsync(id, UserId, request, cancellationToken);
        return Ok(member);
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(string id, string userId, CancellationToken cancellationToken)
    {
        await _membershipService.RemoveMemberAsync(id, UserId, userId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id}/members/{userId}/role")]
    public async Task<ActionResult<HouseholdMemberDto>> UpdateMemberRole(string id, string userId, UpdateMemberRoleRequest request, CancellationToken cancellationToken)
    {
        var member = await _membershipService.UpdateMemberRoleAsync(id, UserId, userId, request, cancellationToken);
        return Ok(member);
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> Leave(string id, CancellationToken cancellationToken)
    {
        await _membershipService.LeaveAsync(id, UserId, cancellationToken);
        return NoContent();
    }

    private string UserId => _currentUserService.UserId!;
}
