namespace NoTiming.Api.Features.Account;

/// <summary>
/// The authentication events of the Api (#601), each with an id and a name that stay as they are, so that a dashboard
/// or an alert can key off them whatever the wording of a message becomes. An event carries the id of the user and what
/// happened. It never carries a code, a cookie, a token, an email address or the body of a mail; a refusal names the
/// limit that was spent and nothing about the client. The ending of sessions is logged by the identity library itself
/// (<c>IdentityEvents.SESSIONS_REVOKED</c>, 1020).
/// </summary>
internal static class AuthEvents
{
    public static readonly EventId CODE_REQUESTED = new(1001, "CodeRequested");
    public static readonly EventId CODE_DELIVERY_FAILED = new(1002, "CodeDeliveryFailed");
    public static readonly EventId CODE_VERIFIED = new(1003, "CodeVerified");
    public static readonly EventId CODE_REFUSED = new(1004, "CodeRefused");
    public static readonly EventId SIGNED_IN = new(1005, "SignedIn");
    public static readonly EventId SIGNED_OUT = new(1006, "SignedOut");
    public static readonly EventId REGISTERED = new(1007, "Registered");
    public static readonly EventId REGISTRATION_DETAILS_DROPPED = new(1008, "RegistrationDetailsDropped");
    public static readonly EventId PASSKEY_ADDED = new(1010, "PasskeyAdded");
    public static readonly EventId PASSKEY_REMOVED = new(1011, "PasskeyRemoved");
    public static readonly EventId PASSKEY_REFUSED = new(1012, "PasskeyRefused");
    public static readonly EventId RATE_LIMITED = new(1030, "RateLimited");
    public static readonly EventId HONEYPOT_FILLED = new(1031, "HoneypotFilled");
    public static readonly EventId REGISTRATION_CLOSED = new(1032, "RegistrationClosed");
    public static readonly EventId TENANT_PLACEMENT_FAILED = new(1040, "TenantPlacementFailed");
}
