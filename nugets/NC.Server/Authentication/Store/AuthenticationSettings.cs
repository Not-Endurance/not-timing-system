namespace NC.Server.Authentication.Store;

/// <summary>Configuration of sign-in: the collections it uses, the session cookie, the passkey relying party and email.</summary>
public sealed class AuthenticationSettings
{
    public const string SECTION = "Authentication";

    public string Database { get; set; } = "nts";
    public string UsersCollection { get; set; } = "users";
    public string SessionsCollection { get; set; } = "sign_in_sessions";
    public string ChallengesCollection { get; set; } = "code_challenges";
    public string RateCountersCollection { get; set; } = "rate_counters";
    public string OutboxCollection { get; set; } = "email_outbox";

    /// <summary>The session cookie. The __Host- prefix makes browsers accept it only as Secure, host-only, at path /.</summary>
    public string CookieName { get; set; } = "__Host-session";

    /// <summary>The passkey relying-party ID. The app origin must be a subdomain of it.</summary>
    public string RelyingPartyId { get; set; } = "localhost";

    /// <summary>The exact origin the app is served from. Passkey ceremonies and state-changing requests are checked against it.</summary>
    public string AppOrigin { get; set; } = "http://app.localhost";

    /// <summary>Keys the code MAC. Required outside Development.</summary>
    public string CodeSecret { get; set; } = string.Empty;

    /// <summary>brevo, console or outbox. Production refuses console and outbox.</summary>
    public string EmailProvider { get; set; } = "outbox";

    public string EmailSenderAddress { get; set; } = "no-reply@localhost";

    public string? BrevoApiKey { get; set; }
}
