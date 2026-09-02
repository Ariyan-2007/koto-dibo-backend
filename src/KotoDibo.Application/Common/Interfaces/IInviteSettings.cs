namespace KotoDibo.Application.Common.Interfaces;

public interface IInviteSettings
{
    // Deep-link base the frontend serves invite codes from, e.g. "https://koto-dibo.ariyan.app/invites".
    // An invite's shareable link is $"{BaseUrl}/{code}".
    string BaseUrl { get; }

    TimeSpan DefaultExpiry { get; }

    TimeSpan MaxExpiry { get; }
}
