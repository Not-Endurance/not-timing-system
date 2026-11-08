namespace NC.Server.Authentication.Codes;

/// <summary>What a code challenge still allows: when it ends, how many checks are left, and whether it was used.</summary>
public sealed record CodeChallengeState
{
    public DateTimeOffset ExpiresAtUtc { get; init; }

    public int AttemptsLeft { get; init; }

    public bool Used { get; init; }
}
