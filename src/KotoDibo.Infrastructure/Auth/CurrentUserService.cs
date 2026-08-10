using System.IdentityModel.Tokens.Jwt;
using KotoDibo.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace KotoDibo.Infrastructure.Auth;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    // Household-scoped authorization lands with the Phase 5 membership model; no household
    // context to resolve yet.
    public string? HouseholdId => null;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
