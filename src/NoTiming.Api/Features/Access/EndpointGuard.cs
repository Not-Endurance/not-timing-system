using Microsoft.AspNetCore.Routing;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The guard that fails closed (ADR-0012, #671): a route of the Api that is neither on the one list of public endpoints nor
/// declares what it needs with <see cref="RequiresAttribute"/> is a route that anybody who is signed in could call, and the
/// host does not start while there is one. A host with endpoints of its own that are public (a Debug build has the local
/// sign in as) says them, as the baseline is told of them. A route about a row of what an Event keeps has to say which rows.
/// </summary>
internal static class EndpointGuard
{
    const string API = "api/";

    /// <summary>
    /// Every route of the Api that is neither public nor guarded, or is about a row and does not say which: how it is written by
    /// <see cref="Describe"/>, and what is wrong with it.
    /// </summary>
    public static IReadOnlyList<string> Mistakes(
        IEnumerable<Endpoint> endpoints,
        IEnumerable<PublicEndpoint>? hostEntries = null
    )
    {
        var mistakes = new List<string>();
        foreach (var route in endpoints.OfType<RouteEndpoint>().Where(IsApi))
        {
            foreach (var method in MethodsOf(route))
            {
                var described = Describe(method, route);
                var requires = route.Metadata.GetMetadata<RequiresAttribute>();
                var isPublic =
                    PublicEndpoints.AccessOf(method, route.RoutePattern.RawText, hostEntries)
                    != EndpointAccess.Protected;
                if (requires == null && !isPublic)
                {
                    mistakes.Add($"{described} declares no requirement and is not public");
                }

                if (requires?.Place == PlaceIn.EventOfRow && route.Metadata.GetMetadata<IEventRows>() == null)
                {
                    mistakes.Add($"{described} is about a row of what an Event keeps and does not say which rows");
                }
            }
        }

        return mistakes;
    }

    public static void ThrowWhenUnguarded(
        IEnumerable<Endpoint> endpoints,
        IEnumerable<PublicEndpoint>? hostEntries = null
    )
    {
        var mistakes = Mistakes(endpoints, hostEntries);
        if (mistakes.Count > 0)
        {
            throw new InvalidOperationException(
                "The Api has routes the access matrix does not know: " + string.Join("; ", mistakes) + "."
            );
        }
    }

    /// <summary>A route as the tests and the list spell it: the method and the pattern without its leading slash.</summary>
    public static string Describe(string method, RouteEndpoint route)
    {
        return $"{method} {route.RoutePattern.RawText!.TrimStart('/')}";
    }

    static IEnumerable<string> MethodsOf(RouteEndpoint route)
    {
        return route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
    }

    static bool IsApi(RouteEndpoint route)
    {
        return route.RoutePattern.RawText?.TrimStart('/').StartsWith(API, StringComparison.Ordinal) == true;
    }
}
