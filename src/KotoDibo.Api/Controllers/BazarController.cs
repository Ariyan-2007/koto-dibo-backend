using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Application.Features.Bazar.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/bazar")]
[Authorize]
public class BazarController : ControllerBase
{
    private readonly IBazarPurchaseService _bazarPurchaseService;
    private readonly ICurrentUserService _currentUserService;

    public BazarController(IBazarPurchaseService bazarPurchaseService, ICurrentUserService currentUserService)
    {
        _bazarPurchaseService = bazarPurchaseService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<ActionResult<BazarPurchaseDto>> Create(string householdId, CreateBazarPurchaseRequest request, CancellationToken cancellationToken)
    {
        var purchase = await _bazarPurchaseService.CreateAsync(householdId, UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { householdId, purchaseId = purchase.Id }, purchase);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BazarPurchaseDto>>> GetList(string householdId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var purchases = await _bazarPurchaseService.GetListAsync(householdId, UserId, from, to, status, cancellationToken);
        return Ok(purchases);
    }

    [HttpGet("{purchaseId}")]
    public async Task<ActionResult<BazarPurchaseDto>> GetById(string householdId, string purchaseId, CancellationToken cancellationToken)
    {
        var purchase = await _bazarPurchaseService.GetByIdAsync(householdId, UserId, purchaseId, cancellationToken);
        return Ok(purchase);
    }

    [HttpPatch("{purchaseId}")]
    public async Task<ActionResult<BazarPurchaseDto>> Update(string householdId, string purchaseId, UpdateBazarPurchaseRequest request, CancellationToken cancellationToken)
    {
        var purchase = await _bazarPurchaseService.UpdateAsync(householdId, UserId, purchaseId, request, cancellationToken);
        return Ok(purchase);
    }

    [HttpPost("{purchaseId}/cancel")]
    public async Task<ActionResult<BazarPurchaseDto>> Cancel(string householdId, string purchaseId, CancellationToken cancellationToken)
    {
        var purchase = await _bazarPurchaseService.CancelAsync(householdId, UserId, purchaseId, cancellationToken);
        return Ok(purchase);
    }

    private string UserId => _currentUserService.UserId!;
}
