namespace NC.Server.Authentication.SignIn;

/// <summary>The answer to a code check. A refusal never says which part was wrong.</summary>
public sealed class VerificationOutcome
{
    public bool SignedIn { get; init; }

    public int UserId { get; init; }

    public string? SessionToken { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
