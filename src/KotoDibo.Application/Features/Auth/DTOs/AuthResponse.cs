namespace KotoDibo.Application.Features.Auth.DTOs;

public record AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
