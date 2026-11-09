using Microsoft.AspNetCore.Http;

namespace NC.Server.Authentication.Passkeys;

/// <summary>
/// The passkeys a signed-in user manages: enrolling, listing, renaming and removing. A different capability from
/// signing in, so it is a separate interface from <see cref="IAuthentication"/>.
/// </summary>
public interface IPasskeyManagement
{
    /// <summary>Options for a new passkey. Null when the user's handle could not be stored, so the caller can ask again.</summary>
    Task<string?> CreationOptions(int userId, CancellationToken cancellationToken);

    /// <summary>Stores the passkey the browser created, and tells the user by email. The request sets the email language.</summary>
    Task<PasskeyOutcome> Enrol(
        HttpContext httpContext,
        int userId,
        string credentialJson,
        string? name,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<PasskeyInfo>> ListPasskeys(int userId, CancellationToken cancellationToken);

    Task<PasskeyOutcome> Rename(int userId, byte[] credentialId, string name, CancellationToken cancellationToken);

    Task<PasskeyOutcome> Remove(int userId, byte[] credentialId, CancellationToken cancellationToken);
}
