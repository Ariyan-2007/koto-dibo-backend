namespace KotoDibo.Application.Features.Households.DTOs;

public record TransferOwnershipRequest
{
    public string NewOwnerUserId { get; init; } = string.Empty;
}
