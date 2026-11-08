namespace NC.Server.Authentication.Sessions;

public sealed class IssuedSessionToken
{
    /// <summary>The secret that goes into the session cookie. It is never stored.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>What the server stores and looks up: the SHA-256 of the token.</summary>
    public string TokenHash { get; init; } = string.Empty;
}
