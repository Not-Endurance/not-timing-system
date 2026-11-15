using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Passkeys;

namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>Answers the browser's ceremony with whatever the test sets: an accepted passkey, or null for a refused one.</summary>
internal sealed class FakePasskeyCeremony : IPasskeyCeremony
{
    public UserPasskeyInfo? Attestation { get; init; }

    public PasskeyAssertion? Assertion { get; init; }

    public Task<string> MakeRequestOptions()
    {
        return Task.FromResult("{}");
    }

    public Task<string> MakeCreationOptions(PasskeyUserEntity user)
    {
        return Task.FromResult("{}");
    }

    public Task<UserPasskeyInfo?> PerformAttestation(string credentialJson)
    {
        return Task.FromResult(Attestation);
    }

    public Task<PasskeyAssertion?> PerformAssertion(string credentialJson)
    {
        return Task.FromResult(Assertion);
    }
}
