using Microsoft.AspNetCore.Identity;

namespace NC.Server.Authentication.Passkeys;

/// <summary>The WebAuthn ceremonies the passkey service needs. The ASP.NET Core sign-in manager implements them in production.</summary>
internal interface IPasskeyCeremony
{
    Task<string> MakeRequestOptions();

    Task<string> MakeCreationOptions(PasskeyUserEntity user);

    /// <summary>The passkey the browser created, when the answer is accepted; otherwise null.</summary>
    Task<UserPasskeyInfo?> PerformAttestation(string credentialJson);

    /// <summary>The user and passkey the browser answered with, when the answer is accepted; otherwise null.</summary>
    Task<PasskeyAssertion?> PerformAssertion(string credentialJson);
}
