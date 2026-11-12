using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Passkeys;

/// <summary>Runs the ceremonies on the ASP.NET Core sign-in manager.</summary>
internal sealed class SignInManagerPasskeyCeremony : IPasskeyCeremony
{
    readonly SignInManager<IdentityUserDocument> _signIn;

    public SignInManagerPasskeyCeremony(SignInManager<IdentityUserDocument> signIn)
    {
        _signIn = signIn;
    }

    public Task<string> MakeRequestOptions()
    {
        return _signIn.MakePasskeyRequestOptionsAsync(null!);
    }

    public Task<string> MakeCreationOptions(PasskeyUserEntity user)
    {
        return _signIn.MakePasskeyCreationOptionsAsync(user);
    }

    public async Task<UserPasskeyInfo?> PerformAttestation(string credentialJson)
    {
        var attestation = await _signIn.PerformPasskeyAttestationAsync(credentialJson);
        return attestation?.Passkey;
    }

    public async Task<PasskeyAssertion?> PerformAssertion(string credentialJson)
    {
        var assertion = await _signIn.PerformPasskeyAssertionAsync(credentialJson);
        if (assertion?.User is null || assertion.Passkey is null)
        {
            return null;
        }

        return new PasskeyAssertion { User = assertion.User, Passkey = assertion.Passkey };
    }
}
