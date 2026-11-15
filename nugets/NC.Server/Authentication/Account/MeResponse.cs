namespace NC.Server.Authentication.Account;

/// <summary>What the app shows of the signed-in person. The profile is read from its own route.</summary>
internal sealed class MeResponse
{
    public static MeResponse Anonymous()
    {
        return new MeResponse { SignedIn = false };
    }

    public static MeResponse For(AuthenticatedUser user)
    {
        return new MeResponse
        {
            SignedIn = true,
            DisplayName = user.DisplayName ?? user.Name ?? user.Email,
            HasPasskey = user.HasPasskey,
        };
    }

    public bool SignedIn { get; init; }

    public string? DisplayName { get; init; }

    public bool HasPasskey { get; init; }
}
