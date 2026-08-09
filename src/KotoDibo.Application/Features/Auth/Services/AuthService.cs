using KotoDibo.Application.Features.Auth.DTOs;
using KotoDibo.Application.Features.Auth.Interfaces;

namespace KotoDibo.Application.Features.Auth.Services;

public class AuthService : IAuthService
{
    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
