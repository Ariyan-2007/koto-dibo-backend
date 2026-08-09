namespace KotoDibo.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? HouseholdId { get; }

    bool IsAuthenticated { get; }
}
