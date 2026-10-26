namespace NoTiming.Api.Features.Access;

/// <summary>
/// The routes of the Api that have not been moved to the mechanism of ADR-0012 yet (#671): they authorize and validate by hand,
/// and <see cref="EndpointGuard"/> lets the host start with them only while they are named here. A family that moves leaves the
/// list in the commit that moves it, and the list ends empty.
/// </summary>
internal static class UnguardedRoutes
{
    public static readonly IReadOnlySet<string> NotYet = new HashSet<string>(StringComparer.Ordinal)
    {
        "DELETE api/passkeys/{id}",
        "DELETE api/user-sessions/{id}",
        "GET api/me",
        "GET api/me/profile",
        "GET api/passkeys",
        "GET api/user-sessions",
        "GET api/user-sessions/{id}",
        "PATCH api/me/profile",
        "PATCH api/passkeys/{id}",
        "PATCH api/user-sessions/{id}",
        "POST api/passkeys",
        "POST api/passkeys/actions/creation-options",
        "POST api/user-sessions",
    };
}
