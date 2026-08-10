using KotoDibo.Application.Common.Interfaces;

namespace KotoDibo.Infrastructure.Auth;

public class RefreshTokenSettings : IRefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    public int ExpiryDays { get; set; } = 30;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(ExpiryDays);
}
