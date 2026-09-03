using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BudgetDashboard.DTOs;
using KotoDibo.Application.Features.BudgetDashboard.Interfaces;
using KotoDibo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

// The Budget & Expenses module's main entry point (Prompt §43) — one call returns everything a
// personal finance dashboard needs: summary totals, budget-vs-actual, category breakdown,
// spending trend, top categories/merchants, overspending, upcoming recurring expenses, a
// period-over-period comparison, and computed insights.
[ApiController]
[Route("api/budget-dashboard")]
[Authorize]
public class BudgetDashboardController : ControllerBase
{
    private readonly IBudgetDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUserService;

    public BudgetDashboardController(IBudgetDashboardService dashboardService, ICurrentUserService currentUserService)
    {
        _dashboardService = dashboardService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(
        [FromQuery] DashboardPeriodPreset? preset,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? budgetId,
        [FromQuery] string? currency,
        [FromQuery] DashboardComparisonPeriod comparisonPeriod = DashboardComparisonPeriod.PreviousPeriod,
        CancellationToken cancellationToken = default)
    {
        var query = new DashboardQuery
        {
            Preset = preset,
            From = from,
            To = to,
            BudgetId = budgetId,
            Currency = currency,
            ComparisonPeriod = comparisonPeriod,
        };

        var dashboard = await _dashboardService.GetDashboardAsync(UserId, query, cancellationToken);
        return Ok(dashboard);
    }

    private string UserId => _currentUserService.UserId!;
}
