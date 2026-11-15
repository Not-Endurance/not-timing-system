namespace NC.Server.Authentication.Passkeys;

/// <summary>A passkey of the signed-in user, as the app lists it. The key material and the attestation stay inside the nuget.</summary>
public sealed class PasskeyInfo
{
    public byte[] CredentialId { get; init; } = [];

    public string? Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
