namespace KotoDibo.Domain.Entities;

public class Household
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
