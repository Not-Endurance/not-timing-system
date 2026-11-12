namespace NC.Server.Authentication.Interface;

/// <summary>The authorization policies the module registers. The app names them in its authorize attributes and route rules.</summary>
public static class AuthenticationPolicies
{
    /// <summary>A user is signed in through the session cookie.</summary>
    public const string SIGNED_IN = "NoTimingSignedIn";
}
