using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The name of an endpoint of the Api is derived from its method and its route, so that a stack trace, a search and a log name
/// the route and nobody has to remember what <c>List</c> was a list of (ADR-0015): the method, then each segment of the route
/// without <c>api</c>, written in Pascal case, with a parameter written <c>By</c> and its name. <c>GET /api/me/profile</c> is
/// <c>GetMeProfile</c>, and <c>PATCH /api/events/{id}/branding</c> is <c>PatchEventsByIdBranding</c>. The JSON:API changes with
/// PATCH and has no PUT.
/// </summary>
internal static class EndpointNames
{
    const string API = "api";
    static readonly IReadOnlyDictionary<string, string> VERBS = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["GET"] = "Get",
        ["POST"] = "Post",
        ["PATCH"] = "Patch",
        ["DELETE"] = "Delete",
    };

    /// <summary>The name of the endpoint that serves the method on the route; none for a route that is not of the Api, or a method it does not use.</summary>
    public static string? Of(string method, string? pattern)
    {
        var segments = pattern?.Trim('/').Split('/');
        if (segments is not [API, _, ..] || !VERBS.TryGetValue(method, out var verb))
        {
            return null;
        }

        return verb + string.Concat(segments.Skip(1).Select(Segment));
    }

    /// <summary>
    /// Every endpoint of the Api that a method handles and whose name is not the one its route gives it, and what it should be
    /// called. The routes that one family of resources makes for each of its members are served by lambdas, which have no name
    /// of their own, and are not held to it.
    /// </summary>
    public static IReadOnlyList<string> Mistakes(IEnumerable<Endpoint> endpoints)
    {
        var mistakes = new List<string>();
        foreach (var route in endpoints.OfType<RouteEndpoint>())
        {
            var pattern = route.RoutePattern.RawText;
            if (
                pattern?.TrimStart('/').StartsWith(API + "/", StringComparison.Ordinal) != true
                || IsUnknownRoute(pattern)
            )
            {
                continue;
            }

            foreach (var method in route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            {
                var expected = Of(method, pattern);
                if (expected is null)
                {
                    mistakes.Add(
                        $"{method} {pattern.TrimStart('/')} uses a method the JSON:API does not: it changes with PATCH"
                    );
                    continue;
                }

                var actual = NameOf(route);
                if (actual is null || actual.Contains('<'))
                {
                    continue;
                }

                if (actual != expected)
                {
                    mistakes.Add($"{method} {pattern.TrimStart('/')} is handled by {actual}: it should be {expected}");
                }
            }
        }

        return mistakes;
    }

    /// <summary>The name of the method that handles the endpoint.</summary>
    static string? NameOf(RouteEndpoint route)
    {
        return route.Metadata.GetMetadata<MethodInfo>()?.Name;
    }

    /// <summary>The answer to a route of the Api that does not exist belongs to the host and takes every method.</summary>
    static bool IsUnknownRoute(string pattern)
    {
        return pattern.TrimStart('/') == "api/{**rest}";
    }

    static string Segment(string segment)
    {
        if (segment.StartsWith('{'))
        {
            var name = segment.Trim('{', '}').TrimStart('*').Split(':', '?')[0];
            return "By" + Pascal(name);
        }

        return Pascal(segment);
    }

    static string Pascal(string text)
    {
        return string.Concat(
            text.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..])
        );
    }
}
