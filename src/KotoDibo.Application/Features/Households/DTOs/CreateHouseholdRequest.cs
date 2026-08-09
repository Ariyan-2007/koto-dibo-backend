namespace KotoDibo.Application.Features.Households.DTOs;

public record CreateHouseholdRequest
{
    public string Name { get; init; } = string.Empty;
}
