namespace KotoDibo.Application.Common.Interfaces;

public interface IInviteSettings
{
    TimeSpan DefaultExpiry { get; }

    TimeSpan MaxExpiry { get; }
}
