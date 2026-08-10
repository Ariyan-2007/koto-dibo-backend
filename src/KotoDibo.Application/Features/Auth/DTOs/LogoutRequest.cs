namespace KotoDibo.Application.Features.Auth.DTOs;

public record LogoutRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
