namespace KotoDibo.Application.Features.Auth.DTOs;

public record RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
