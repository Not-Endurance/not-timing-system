namespace NC.Server.Authentication.Identity;

/// <summary>A passkey, embedded in its user's document with the fields Identity needs to check an assertion.</summary>
internal sealed class IdentityPasskeyDocument
{
    public byte[] CredentialId { get; set; } = [];

    public byte[] PublicKey { get; set; } = [];

    public string? Name { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public long SignCount { get; set; }

    public string[] Transports { get; set; } = [];

    public bool IsUserVerified { get; set; }

    public bool IsBackupEligible { get; set; }

    public bool IsBackedUp { get; set; }

    public byte[] AttestationObject { get; set; } = [];

    public byte[] ClientDataJson { get; set; } = [];
}
