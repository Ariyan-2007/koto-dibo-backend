using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BillSplit.DTOs;
using KotoDibo.Application.Features.BillSplit.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/bill-splits")]
[Authorize]
public class BillSplitController : ControllerBase
{
    private readonly IBillSplitService _billSplitService;
    private readonly ICurrentUserService _currentUserService;

    public BillSplitController(IBillSplitService billSplitService, ICurrentUserService currentUserService)
    {
        _billSplitService = billSplitService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<ActionResult<BillSplitDto>> Create(string householdId, CreateBillSplitRequest request, CancellationToken cancellationToken)
    {
        var billSplit = await _billSplitService.CreateAsync(householdId, UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { householdId, billSplitId = billSplit.Id }, billSplit);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BillSplitDto>>> GetList(string householdId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var billSplits = await _billSplitService.GetListAsync(householdId, UserId, from, to, status, cancellationToken);
        return Ok(billSplits);
    }

    [HttpGet("{billSplitId}")]
    public async Task<ActionResult<BillSplitDto>> GetById(string householdId, string billSplitId, CancellationToken cancellationToken)
    {
        var billSplit = await _billSplitService.GetByIdAsync(householdId, UserId, billSplitId, cancellationToken);
        return Ok(billSplit);
    }

    [HttpGet("{billSplitId}/settlement")]
    public async Task<ActionResult<BillSplitSettlementDto>> GetSettlement(string householdId, string billSplitId, CancellationToken cancellationToken)
    {
        var settlement = await _billSplitService.GetSettlementAsync(householdId, UserId, billSplitId, cancellationToken);
        return Ok(settlement);
    }

    [HttpPatch("{billSplitId}")]
    public async Task<ActionResult<BillSplitDto>> Update(string householdId, string billSplitId, UpdateBillSplitRequest request, CancellationToken cancellationToken)
    {
        var billSplit = await _billSplitService.UpdateAsync(householdId, UserId, billSplitId, request, cancellationToken);
        return Ok(billSplit);
    }

    [HttpPost("{billSplitId}/cancel")]
    public async Task<ActionResult<BillSplitDto>> Cancel(string householdId, string billSplitId, CancellationToken cancellationToken)
    {
        var billSplit = await _billSplitService.CancelAsync(householdId, UserId, billSplitId, cancellationToken);
        return Ok(billSplit);
    }

    private string UserId => _currentUserService.UserId!;
}
