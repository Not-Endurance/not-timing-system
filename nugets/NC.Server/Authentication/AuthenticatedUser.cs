using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Profiles;

namespace NC.Server.Authentication;

/// <summary>The signed-in person's identity as the app shows it. It holds no storage detail: no hashes, stamps or key material.</summary>
public class AuthenticatedUser
{
    public int Id { get; init; }

    public string? Email { get; init; }

    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? GivenName { get; init; }

    public string? MiddleName { get; init; }

    public string? Surname { get; init; }

    public string[] Roles { get; init; } = [];

    public bool HasPasskey { get; init; }
}

/// <summary>
/// The signed-in person with the app's profile. The app defines <typeparamref name="TProfile"/>: its fields are the app's to
/// choose, and the module stores them without knowing their names.
/// </summary>
public sealed class AuthenticatedUser<TProfile> : AuthenticatedUser
    where TProfile : class, new()
{
    public TProfile Profile { get; init; } = new();

    internal static AuthenticatedUser<TProfile> From(IdentityUserDocument user)
    {
        return new AuthenticatedUser<TProfile>
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            MiddleName = user.MiddleName,
            Surname = user.Surname,
            Roles = [.. user.Roles],
            HasPasskey = user.Passkeys is { Count: > 0 },
            Profile = ProfileDocument.FromBson<TProfile>(user.Profile),
        };
    }
}
