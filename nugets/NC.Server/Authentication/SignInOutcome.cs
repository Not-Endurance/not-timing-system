namespace NC.Server.Authentication;

/// <summary>
/// The answer to a sign-in. A refusal carries no reason: every failed sign-in gets the same answer, so a caller cannot
/// tell an unknown address from a wrong code.
/// </summary>
public sealed class SignInOutcome<TProfile>
    where TProfile : class, new()
{
    public bool SignedIn { get; init; }

    /// <summary>The user who is now signed in. Null when <see cref="SignedIn"/> is false.</summary>
    public AuthenticatedUser<TProfile>? User { get; init; }
}
