using KotoDibo.Application.Common.Interfaces;

namespace KotoDibo.Infrastructure.Common;

public class InviteSettings : IInviteSettings
{
    public const string SectionName = "Invites";

    public int DefaultExpiryHours { get; set; } = 168;
    public int MaxExpiryHours { get; set; } = 720;

    public TimeSpan DefaultExpiry => TimeSpan.FromHours(DefaultExpiryHours);

    public TimeSpan MaxExpiry => TimeSpan.FromHours(MaxExpiryHours);

    // Populated from Cors:AllowedOrigins in ServiceCollectionExtensions rather than bound directly
    // from the Invites config section — the trusted-frontend-origin list is the same set either
    // way, so it's kept as one source of truth instead of two config entries that could drift apart.
    public List<string> AllowedBaseUrls { get; set; } = [];

    IReadOnlyList<string> IInviteSettings.AllowedBaseUrls => AllowedBaseUrls;
}
