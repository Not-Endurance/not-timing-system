using Microsoft.AspNetCore.Identity;

namespace NC.Server.Authentication.Passkeys;

/// <summary>Passkeys for sign-in, and the passkeys a signed-in user manages.</summary>
public interface IPasskeyService
{
    Task<string> RequestOptions();

    Task<string?> CreationOptions(int userId, CancellationToken cancellationToken);

    Task<PasskeyOutcome> Enrol(
        int userId,
        string credentialJson,
        string? name,
        string language,
        CancellationToken cancellationToken
    );

    Task<PasskeySignIn?> SignIn(string credentialJson, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserPasskeyInfo>> ListPasskeys(int userId, CancellationToken cancellationToken);

    Task<PasskeyOutcome> Rename(int userId, byte[] credentialId, string name, CancellationToken cancellationToken);

    Task<PasskeyOutcome> Remove(int userId, byte[] credentialId, CancellationToken cancellationToken);
}
