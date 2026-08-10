using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.MealCalculation.DTOs;
using KotoDibo.Application.Features.MealCalculation.Interfaces;
using KotoDibo.Application.Features.Meals.DTOs;
using KotoDibo.Application.Features.Meals.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/households/{householdId}/meals")]
[Authorize]
public class MealsController : ControllerBase
{
    private readonly IDailyMealEntryService _mealEntryService;
    private readonly IMealCalculationService _calculationService;
    private readonly ICurrentUserService _currentUserService;

    public MealsController(
        IDailyMealEntryService mealEntryService,
        IMealCalculationService calculationService,
        ICurrentUserService currentUserService)
    {
        _mealEntryService = mealEntryService;
        _calculationService = calculationService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DailyMealEntryDto>>> GetList(string householdId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? userId, CancellationToken cancellationToken)
    {
        var entries = await _mealEntryService.GetListAsync(householdId, UserId, from, to, userId, cancellationToken);
        return Ok(entries);
    }

    [HttpGet("rate")]
    public async Task<ActionResult<MealCalculationDto>> GetRate(string householdId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var result = await _calculationService.GetMealRateAsync(householdId, UserId, from, to, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{date}/{userId}")]
    public async Task<ActionResult<DailyMealEntryDto>> SetCount(string householdId, DateOnly date, string userId, SetMealCountRequest request, CancellationToken cancellationToken)
    {
        var entry = await _mealEntryService.SetCountAsync(householdId, UserId, userId, date, request, cancellationToken);
        return Ok(entry);
    }

    [HttpDelete("{date}/{userId}")]
    public async Task<IActionResult> Remove(string householdId, DateOnly date, string userId, CancellationToken cancellationToken)
    {
        await _mealEntryService.RemoveAsync(householdId, UserId, userId, date, cancellationToken);
        return NoContent();
    }

    private string UserId => _currentUserService.UserId!;
}
