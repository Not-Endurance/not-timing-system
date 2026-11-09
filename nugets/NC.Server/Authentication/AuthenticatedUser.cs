using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication;

/// <summary>The signed-in person as the app shows them. It holds no storage detail: no hashes, stamps or key material.</summary>
public sealed class AuthenticatedUser
{
    public int Id { get; init; }

    public string? Email { get; init; }

    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? GivenName { get; init; }

    public string? MiddleName { get; init; }

    public string? Surname { get; init; }

    public string? CountryRegion { get; init; }

    public string? Club { get; init; }

    public string? FeiId { get; init; }

    public string[] Roles { get; init; } = [];

    public bool HasPasskey { get; init; }

    internal static AuthenticatedUser From(IdentityUserDocument user)
    {
        return new AuthenticatedUser
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            MiddleName = user.MiddleName,
            Surname = user.Surname,
            CountryRegion = user.CountryRegion,
            Club = user.Club,
            FeiId = user.FeiId,
            Roles = [.. user.Roles],
            HasPasskey = user.Passkeys is { Count: > 0 },
        };
    }
}
