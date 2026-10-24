using Microsoft.AspNetCore.Routing;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// Applies <see cref="AccessBaseline"/> to every request that has an endpoint, after the session has been read and
/// before anything else runs. It answers in the format of the rest-api skill: a caller who is not signed in is 401
/// <c>not-signed-in</c>, never a redirect, and a write without the header is 400 <c>request-header-required</c>. A
/// request that has no endpoint reaches nothing and is left to answer 404.
/// </summary>
internal sealed class AccessBaselineMiddleware
{
    readonly RequestDelegate _next;

    public AccessBaselineMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint endpoint)
        {
            var method = context.Request.Method;
            var decision = AccessBaseline.Decide(
                method,
                PublicEndpoints.AccessOf(method, endpoint.RoutePattern.RawText),
                context.User.Identity?.IsAuthenticated == true,
                context.Request.Headers[AccessBaseline.WRITE_HEADER].ToString()
            );
            if (decision != AccessDecision.Allow)
            {
                await Refusal(decision).ExecuteAsync(context);
                return;
            }
        }

        await _next(context);
    }

    static IResult Refusal(AccessDecision decision)
    {
        return decision == AccessDecision.NotSignedIn
            ? JsonApiResults.NotSignedIn()
            : JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "request-header-required",
                "Send the header the pages send with every write.",
                $"{AccessBaseline.WRITE_HEADER}: {AccessBaseline.WRITE_HEADER_VALUE}"
            );
    }
}
