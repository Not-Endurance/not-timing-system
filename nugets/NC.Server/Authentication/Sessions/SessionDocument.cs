namespace NC.Server.Authentication.Sessions;

/// <summary>A server-side session. Only the hash of the token is stored, so a copy of the database cannot be replayed.</summary>
public sealed class SessionDocument
{
    public string TokenHash { get; set; } = string.Empty;

    public int UserId { get; set; }

    /// <summary>The user's security stamp when the session began. A changed stamp ends the session.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastExtendedAtUtc { get; set; }

    /// <summary>The TTL index deletes the session once this has passed.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
