namespace KotoDibo.Application.Common.Interfaces;

public interface IInviteSettings
{
    TimeSpan DefaultExpiry { get; }

    TimeSpan MaxExpiry { get; }

    // Origins (scheme + host [+ port]) the client-supplied CreateHouseholdInviteRequest.BaseUrl is
    // allowed to point at. Without this, any household member with AddMember permission could make
    // the server email/QR-encode a link to an attacker-controlled domain under the guise of a
    // trusted "You're invited to join ... on Koto Dibo" message — see HouseholdInviteService.
    IReadOnlyList<string> AllowedBaseUrls { get; }
}
