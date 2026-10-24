namespace Not.Identity;

public sealed class NIdentityOptions
{
    /// <summary>The database that holds the user documents, the sessions and the code challenges.</summary>
    public string Database { get; set; } = "nts";

    /// <summary>The application's own user documents, which identity extends in place (ADR-0002).</summary>
    public string UsersCollection { get; set; } = "users";

    public string SessionsCollection { get; set; } = "auth_sessions";
    public string ChallengesCollection { get; set; } = "auth_challenges";

    /// <summary>
    /// The session cookie. The <c>__Host-</c> prefix makes browsers accept it only as Secure, for the host alone and
    /// for the whole path.
    /// </summary>
    public string CookieName { get; set; } = "__Host-session";

    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>The cookie that holds the challenge of a passkey ceremony for the minutes it takes.</summary>
    public string CeremonyCookieName { get; set; } = "__Host-ceremony";

    public TimeSpan CeremonyLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often a session is checked against the user's security stamp. Rotating the stamp also deletes the user's
    /// sessions at once, so this only covers a stamp that changed by another route.
    /// </summary>
    public TimeSpan StampValidationInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public int CodeMaxAttempts { get; set; } = 5;
    public TimeSpan CodeResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
