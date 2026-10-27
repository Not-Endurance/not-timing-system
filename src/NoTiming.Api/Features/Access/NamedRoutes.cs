namespace NoTiming.Api.Features.Access;

/// <summary>
/// The routes that one family of resources makes for each of its members (the registries, what an Event keeps, the links of an
/// Athlete and of a Horse) are served by lambdas, which have no name of their own. These name the route as the method that
/// serves it would be named (<see cref="EndpointNames"/>), so that the name is derived and nobody types it, and so that a test
/// can hold every route of the Api to the same rule.
/// </summary>
internal static class NamedRoutes
{
    public static RouteHandlerBuilder MapNamedGet(this IEndpointRouteBuilder app, string pattern, Delegate handler)
    {
        return app.MapGet(pattern, handler).WithName(NameOf("GET", pattern));
    }

    public static RouteHandlerBuilder MapNamedPost(this IEndpointRouteBuilder app, string pattern, Delegate handler)
    {
        return app.MapPost(pattern, handler).WithName(NameOf("POST", pattern));
    }

    public static RouteHandlerBuilder MapNamedPatch(this IEndpointRouteBuilder app, string pattern, Delegate handler)
    {
        return app.MapPatch(pattern, handler).WithName(NameOf("PATCH", pattern));
    }

    public static RouteHandlerBuilder MapNamedDelete(this IEndpointRouteBuilder app, string pattern, Delegate handler)
    {
        return app.MapDelete(pattern, handler).WithName(NameOf("DELETE", pattern));
    }

    static string NameOf(string method, string pattern)
    {
        return EndpointNames.Of(method, pattern)
            ?? throw new ArgumentException($"{method} {pattern} is not a route of the JSON:API.", nameof(pattern));
    }
}
