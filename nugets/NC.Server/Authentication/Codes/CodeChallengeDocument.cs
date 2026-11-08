namespace NC.Server.Authentication.Codes;

/// <summary>One challenge per normalized address. It holds the MAC of the code, never the code.</summary>
public sealed class CodeChallengeDocument
{
    /// <summary>The normalized address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>sign-in or register.</summary>
    public string Purpose { get; set; } = string.Empty;

    public byte[] Mac { get; set; } = [];

    public int AttemptsLeft { get; set; }

    public DateTime LastSentAtUtc { get; set; }

    /// <summary>The TTL index deletes the challenge once this has passed.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    public RegistrationDetailsDocument? Registration { get; set; }
}
