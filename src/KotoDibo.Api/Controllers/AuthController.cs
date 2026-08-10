using KotoDibo.Api.Extensions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Auth.DTOs;
using KotoDibo.Application.Features.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(ServiceCollectionExtensions.AuthRateLimiterPolicy)]
public class AuthController : ControllerBase
{
    // Generous relative to the largest legitimate payload (name/email/password/token strings),
    // tight enough to reject oversized bodies before they reach model binding / validation.
    private const long MaxRequestBodyBytes = 8 * 1024;

    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(IAuthService authService, ICurrentUserService currentUserService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
    }

    [HttpPost("register")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, ClientIp, UserAgent, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, ClientIp, UserAgent, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshAsync(request, ClientIp, UserAgent, cancellationToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _authService.LogoutAllAsync(_currentUserService.UserId!, cancellationToken);
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } value ? value : null;
}
