using Microsoft.AspNetCore.Routing;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The guard that fails closed (ADR-0012, #671): a route of the Api that is neither on the one list of public endpoints nor
/// declares what it needs with <see cref="RequiresAttribute"/> is a route that anybody who is signed in could call, and the
/// host does not start while there is one. The routes that have not been moved to the mechanism yet are named in
/// <see cref="NotYet"/>, which only ever shrinks, and a route in it that is not there any more, or that has been moved, is
/// as much a mistake as one that is missing.
/// </summary>
internal static class EndpointGuard
{
    const string API = "api/";

    /// <summary>
    /// Every route of the Api that is neither public nor guarded: how it is written by <see cref="Describe"/>. A mistake is a route
    /// that is here and not in <paramref name="notYet"/>, or in <paramref name="notYet"/> and guarded already or not there.
    /// </summary>
    public static IReadOnlyList<string> Mistakes(IEnumerable<Endpoint> endpoints, IReadOnlySet<string> notYet)
    {
        var routes = endpoints.OfType<RouteEndpoint>().Where(IsApi).ToList();
        var mistakes = new List<string>();
        foreach (var route in routes)
        {
            foreach (var method in MethodsOf(route))
            {
                var described = Describe(method, route);
                var guarded = route.Metadata.GetMetadata<RequiresAttribute>() != null;
                var isPublic = PublicEndpoints.AccessOf(method, route.RoutePattern.RawText) != EndpointAccess.Protected;
                var listed = notYet.Contains(described);
                if (!guarded && !isPublic && !listed)
                {
                    mistakes.Add($"{described} declares no requirement and is not public");
                }
                else if ((guarded || isPublic) && listed)
                {
                    mistakes.Add($"{described} is guarded already and is still named as not yet guarded");
                }
            }
        }

        var described_ = routes
            .SelectMany(route => MethodsOf(route).Select(method => Describe(method, route)))
            .ToHashSet();
        mistakes.AddRange(
            notYet
                .Where(x => !described_.Contains(x))
                .Select(x => $"{x} is named as not yet guarded and is not a route")
        );
        return mistakes;
    }

    public static void ThrowWhenUnguarded(IEnumerable<Endpoint> endpoints, IReadOnlySet<string> notYet)
    {
        var mistakes = Mistakes(endpoints, notYet);
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
