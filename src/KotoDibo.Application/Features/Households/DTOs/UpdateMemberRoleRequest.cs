namespace KotoDibo.Application.Features.Households.DTOs;

public record UpdateMemberRoleRequest
{
    public string Role { get; init; } = string.Empty;
}
