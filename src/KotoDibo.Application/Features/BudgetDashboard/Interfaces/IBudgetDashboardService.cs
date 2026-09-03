using KotoDibo.Application.Features.BudgetDashboard.DTOs;

namespace KotoDibo.Application.Features.BudgetDashboard.Interfaces;

public interface IBudgetDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(string userId, DashboardQuery query, CancellationToken cancellationToken = default);
}
