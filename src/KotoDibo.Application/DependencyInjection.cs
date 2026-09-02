using System.Reflection;
using FluentValidation;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Auth.Interfaces;
using KotoDibo.Application.Features.Auth.Services;
using KotoDibo.Application.Features.Bazar.Interfaces;
using KotoDibo.Application.Features.Bazar.Services;
using KotoDibo.Application.Features.BillSplit.Interfaces;
using KotoDibo.Application.Features.BillSplit.Services;
using KotoDibo.Application.Features.Budget.Interfaces;
using KotoDibo.Application.Features.Budget.Services;
using KotoDibo.Application.Features.Contributions.Interfaces;
using KotoDibo.Application.Features.Contributions.Services;
using KotoDibo.Application.Features.Expenses.Interfaces;
using KotoDibo.Application.Features.Expenses.Services;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
using KotoDibo.Application.Features.HouseholdBalance.Services;
using KotoDibo.Application.Features.Households.Interfaces;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Application.Features.MealCalculation.Interfaces;
using KotoDibo.Application.Features.MealCalculation.Services;
using KotoDibo.Application.Features.Meals.Interfaces;
using KotoDibo.Application.Features.Meals.Services;
using KotoDibo.Application.Features.Settlement.Interfaces;
using KotoDibo.Application.Features.Settlement.Services;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace KotoDibo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(assembly);
        services.AddSingleton(typeAdapterConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IHouseholdAccessService, HouseholdAccessService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IHouseholdMembershipService, HouseholdMembershipService>();
        services.AddScoped<IDailyMealEntryService, DailyMealEntryService>();
        services.AddScoped<IMealCalculationService, MealCalculationService>();
        services.AddScoped<IBazarPurchaseService, BazarPurchaseService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<IHouseholdBalanceService, HouseholdBalanceService>();
        services.AddScoped<IBillSplitService, BillSplitService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IBudgetService, BudgetService>();

        return services;
    }
}
