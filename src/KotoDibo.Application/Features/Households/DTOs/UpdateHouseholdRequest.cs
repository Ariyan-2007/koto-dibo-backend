namespace KotoDibo.Application.Features.Households.DTOs;

public record UpdateHouseholdRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Type { get; init; }
}
