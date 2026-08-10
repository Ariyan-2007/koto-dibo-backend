namespace KotoDibo.Application.Features.Households.DTOs;

public record AddMemberRequest
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
