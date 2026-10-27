using System.Text.Json;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Tenancy;

namespace NoTiming.Api.Features.Account;

internal sealed class CodeChallengeAttributes
{
    public string? Email { get; set; }
}

internal sealed class SessionAttributes
{
    public string? Email { get; set; }
    public string? Code { get; set; }

    /// <summary>The assertion a passkey made, as the JSON of the browser's WebAuthn API: the other way to sign in.</summary>
    public JsonElement? Credential { get; set; }
}
