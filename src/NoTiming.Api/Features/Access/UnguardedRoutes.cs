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
        "DELETE api/athletes/{id}",
        "DELETE api/athletes/{id}/link",
        "DELETE api/clubs/{id}",
        "DELETE api/horses/{id}",
        "DELETE api/horses/{id}/link",
        "DELETE api/passkeys/{id}",
        "DELETE api/user-sessions/{id}",
        "GET api/accounts",
        "GET api/accounts/all-tenants",
        "GET api/athletes",
        "GET api/athletes/all-tenants",
        "GET api/athletes/{id}",
        "GET api/athletes/{id}/link",
        "GET api/clubs",
        "GET api/clubs/all-tenants",
        "GET api/clubs/{id}",
        "GET api/countries",
        "GET api/countries/{id}",
        "GET api/horses",
        "GET api/horses/all-tenants",
        "GET api/horses/{id}",
        "GET api/horses/{id}/link",
        "GET api/me",
        "GET api/me/profile",
        "GET api/officials/all-tenants",
        "GET api/participations/history",
        "GET api/passkeys",
        "GET api/user-sessions",
        "GET api/user-sessions/{id}",
        "PATCH api/athletes/{id}",
        "PATCH api/clubs/{id}",
        "PATCH api/countries/{id}",
        "PATCH api/horses/{id}",
        "PATCH api/me/profile",
        "PATCH api/passkeys/{id}",
        "PATCH api/user-sessions/{id}",
        "POST api/athletes",
        "POST api/athletes/{id}/link",
        "POST api/clubs",
        "POST api/countries",
        "POST api/horses",
        "POST api/horses/{id}/link",
        "POST api/passkeys",
        "POST api/passkeys/actions/creation-options",
        "POST api/user-sessions",
    };
}
