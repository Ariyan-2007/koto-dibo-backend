using KotoDibo.Application.Features.Auth.DTOs;

namespace KotoDibo.Application.Features.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default);
}
