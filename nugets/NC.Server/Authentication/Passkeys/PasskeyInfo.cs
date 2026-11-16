namespace NC.Server.Authentication.Passkeys;

/// <summary>A passkey of the signed-in user, as the app lists it. The key material and the attestation stay inside the nuget.</summary>
public sealed class PasskeyInfo
{
    public byte[] CredentialId { get; init; } = [];

    public string? Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the passkey last signed the user in. Null while it has never been used to sign in.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
