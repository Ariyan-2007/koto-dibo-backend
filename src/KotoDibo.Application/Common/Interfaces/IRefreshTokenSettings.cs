namespace KotoDibo.Application.Common.Interfaces;

public interface IRefreshTokenSettings
{
    TimeSpan RefreshTokenLifetime { get; }
}
