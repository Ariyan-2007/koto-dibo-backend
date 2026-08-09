namespace KotoDibo.Domain.Entities;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? HouseholdId { get; set; }
    public DateTime CreatedAt { get; set; }
}
