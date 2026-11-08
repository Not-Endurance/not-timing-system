namespace NC.Server.Authentication.Passkeys;

public sealed class PasskeySignIn
{
    public int UserId { get; init; }

    public string SessionToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
