namespace KotoDibo.Application.Features.Auth.DTOs;

public record RegisterRequest
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? DeviceId { get; init; }
    public string? DeviceName { get; init; }
}
