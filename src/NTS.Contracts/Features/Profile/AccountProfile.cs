namespace NTS.Contracts.Features.Profile;

/// <summary>
/// The profile of the person signed in, as <c>GET /api/me/profile</c> tells it (#602): the names, the country, the club and
/// the FEI ID the host keeps. A profile is complete when it names a first name, a surname and a country, which sending a
/// Snapshot asks for (ADR-0001) and nothing else does.
/// </summary>
public sealed class AccountProfile
{
    public string? GivenName { get; init; }
    public string? MiddleName { get; init; }
    public string? Surname { get; init; }

    /// <summary>The country the profile names, when it is one of ours.</summary>
    public Guid? CountryId { get; init; }

    public string? CountryRegion { get; init; }
    public string? Club { get; init; }
    public string? FeiId { get; init; }
    public bool Complete { get; init; }
}
