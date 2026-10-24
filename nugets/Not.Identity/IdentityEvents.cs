using Microsoft.Extensions.Logging;

namespace Not.Identity;

/// <summary>
/// The events that the identity library logs itself. The application that uses it logs the others, and numbers them
/// from another range (the Api's authentication events are 1001 to 1040).
/// </summary>
public static class IdentityEvents
{
    /// <summary>The sessions of a user were ended, as a change of the security stamp does.</summary>
    public static readonly EventId SESSIONS_REVOKED = new(1020, "SessionsRevoked");
}
