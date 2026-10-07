using NTS.Contracts;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// One public endpoint of the Api: the methods an anonymous caller may use on it and its route as it is mapped.
/// </summary>
internal sealed class PublicEndpoint
{
    const string ANY = "*";

    public PublicEndpoint(EndpointAccess access, string methods, string pattern)
    {
        Access = access;
        Methods = methods.Split(',');
        Pattern = pattern.TrimStart('/');
    }

    public EndpointAccess Access { get; }
    public IReadOnlyList<string> Methods { get; }
    public string Pattern { get; }

    public bool Matches(string method, string pattern)
    {
        return string.Equals(Pattern, pattern.TrimStart('/'), StringComparison.Ordinal)
            && Methods.Any(x => x == ANY || string.Equals(x, method, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// The one list of what an anonymous caller may reach (#602, ADR-0001, ADR-0012). Everything else needs a session, and
/// nothing else can make an endpoint public: a route is not public because it says so, only because it is named here,
/// and a test enumerates the real endpoints against this list. Public reads are reads; the exceptions are the live hub
/// and its negotiation, which only push what any viewer may see and have no method a client can call, and the answer
/// to an unknown API route, which is a 404 for everybody. The routes where a person signs in or registers are
/// anonymous writes, because they are how anyone becomes a caller.
/// </summary>
internal static class PublicEndpoints
{
    /// <summary>
    /// How the route is reached; protected for any that the list does not name. A host that has endpoints of its own, which a
    /// Debug build has for the local sign in as, says them as <paramref name="hostEntries"/>: a deployed host has none.
    /// </summary>
    public static EndpointAccess AccessOf(
        string method,
        string? routePattern,
        IEnumerable<PublicEndpoint>? hostEntries = null
    )
    {
        if (routePattern == null)
        {
            return EndpointAccess.Protected;
        }

        return All.Concat(hostEntries ?? []).FirstOrDefault(x => x.Matches(method, routePattern))?.Access
            ?? EndpointAccess.Protected;
    }

    public static IReadOnlyList<PublicEndpoint> All { get; } = Create();

    static List<PublicEndpoint> Create()
    {
        return
        [
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "healthz"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "past-events"), // sent on to the historic events
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "past-events/{eventId:guid}"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/events"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/events/live"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/events/historic"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/events/{id}"),
            // what an Event keeps is what its public views show: Participations, Rankings, Officials and Handouts
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/participations"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/participations/{id}"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/rankings"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/rankings/{id}"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/officials"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/officials/{id}"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/handouts"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET", "api/handouts/{id}"),
            new PublicEndpoint(EndpointAccess.PublicRead, "GET,HEAD", "{**path:nonfile}"), // the Ui
            new PublicEndpoint(EndpointAccess.PublicRead, "*", ApplicationConstants.LIVE_HUB),
            new PublicEndpoint(EndpointAccess.PublicRead, "*", ApplicationConstants.LIVE_HUB + "/negotiate"),
            new PublicEndpoint(EndpointAccess.PublicRead, "*", "api/{**rest}"), // an unknown route: 404 for everybody
            new PublicEndpoint(EndpointAccess.SignIn, "GET,HEAD", "sign-in"),
            new PublicEndpoint(EndpointAccess.SignIn, "GET,HEAD", "register"),
            new PublicEndpoint(EndpointAccess.SignIn, "GET,HEAD", "privacy"),
            new PublicEndpoint(EndpointAccess.SignIn, "GET,HEAD", "account/assets/{name}"),
            new PublicEndpoint(EndpointAccess.SignIn, "GET,HEAD", "account/passkeys"), // sends the visitor to sign in
            new PublicEndpoint(EndpointAccess.SignIn, "POST", "api/code-challenges"),
            new PublicEndpoint(EndpointAccess.SignIn, "POST", "api/sessions"),
            new PublicEndpoint(EndpointAccess.SignIn, "DELETE", "api/sessions/current"),
            new PublicEndpoint(EndpointAccess.SignIn, "POST", "api/registrations"),
            new PublicEndpoint(EndpointAccess.SignIn, "POST", "api/passkeys/actions/request-options"),
        ];
    }
}
