using Microsoft.AspNetCore.Identity;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.Passkeys;

/// <summary>The WebAuthn ceremonies the passkey service needs. The ASP.NET Core sign-in manager implements them in production.</summary>
public interface IPasskeyCeremony
{
    Task<string> MakeRequestOptionsAsync();

    Task<string> MakeCreationOptionsAsync(PasskeyUserEntity user);

    /// <summary>The passkey the browser created, when the answer is accepted; otherwise null.</summary>
    Task<UserPasskeyInfo?> PerformAttestationAsync(string credentialJson);

    /// <summary>The user and passkey the browser answered with, when the answer is accepted; otherwise null.</summary>
    Task<PasskeyAssertion?> PerformAssertionAsync(string credentialJson);
}

public sealed class PasskeyAssertion
{
    public IdentityUserDocument User { get; init; } = new();

    public UserPasskeyInfo Passkey { get; init; } = new(
        [],
        [],
        DateTimeOffset.UnixEpoch,
        0,
        [],
        false,
        false,
        false,
        [],
        []
    );
}

/// <summary>Starts the session of a user who signed in with a passkey. The session store implements it.</summary>
public interface IPasskeySessionStore
{
    Task CreateAsync(string tokenHash, int userId, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>The stored users a passkey needs. The identity user store implements it.</summary>
public interface IPasskeyUsers
{
    Task<IdentityUserDocument?> FindByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task<IdentityResult> ChangeAsync(
        int userId,
        Func<IdentityUserDocument, Task<IdentityResult>> change,
        CancellationToken cancellationToken = default
    );

    Task<IdentityResult> UpdateAsync(IdentityUserDocument user, CancellationToken cancellationToken);

    Task AddOrUpdatePasskeyAsync(IdentityUserDocument user, UserPasskeyInfo passkey, CancellationToken cancellationToken);

    Task<IList<UserPasskeyInfo>> GetPasskeysAsync(IdentityUserDocument user, CancellationToken cancellationToken);

    Task<bool> RemovePasskeyWhileAnotherRemainsAsync(
        int userId,
        byte[] credentialId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Runs the ceremonies on the ASP.NET Core sign-in manager.</summary>
public sealed class SignInManagerPasskeyCeremony : IPasskeyCeremony
{
    readonly SignInManager<IdentityUserDocument> _signIn;

    public SignInManagerPasskeyCeremony(SignInManager<IdentityUserDocument> signIn)
    {
        _signIn = signIn;
    }

    public Task<string> MakeRequestOptionsAsync()
    {
        return _signIn.MakePasskeyRequestOptionsAsync(null!);
    }

    public Task<string> MakeCreationOptionsAsync(PasskeyUserEntity user)
    {
        return _signIn.MakePasskeyCreationOptionsAsync(user);
    }

    public async Task<UserPasskeyInfo?> PerformAttestationAsync(string credentialJson)
    {
        var attestation = await _signIn.PerformPasskeyAttestationAsync(credentialJson);
        return attestation?.Passkey;
    }

    public async Task<PasskeyAssertion?> PerformAssertionAsync(string credentialJson)
    {
        var assertion = await _signIn.PerformPasskeyAssertionAsync(credentialJson);
        if (assertion?.User is null || assertion.Passkey is null)
        {
            return null;
        }

        return new PasskeyAssertion { User = assertion.User, Passkey = assertion.Passkey };
    }
}
