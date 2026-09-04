using KotoDibo.Application.Common.Interfaces;

namespace KotoDibo.Infrastructure.Common;

public class InviteSettings : IInviteSettings
{
    public const string SectionName = "Invites";

    public int DefaultExpiryHours { get; set; } = 168;
    public int MaxExpiryHours { get; set; } = 720;

    public TimeSpan DefaultExpiry => TimeSpan.FromHours(DefaultExpiryHours);

    public TimeSpan MaxExpiry => TimeSpan.FromHours(MaxExpiryHours);
}
