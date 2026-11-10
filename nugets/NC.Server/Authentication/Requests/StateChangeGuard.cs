using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Settings;

namespace NC.Server.Authentication.Requests;

/// <summary>
/// Guards the state-changing requests of the sign-in routes against cross-site requests. A request must carry a custom
/// header, which a browser sends cross-origin only after a preflight, and it must not be marked cross-site or come from
/// an origin other than the app's. SameSite alone does not stop a sibling subdomain, which counts as same-site.
/// </summary>
internal sealed class StateChangeGuard : IEndpointFilter
{
    public const string REQUEST_HEADER = "X-NTS-Request";

    readonly IOptions<AuthenticationSettings> _settings;

    public StateChangeGuard(IOptions<AuthenticationSettings> settings)
    {
        _settings = settings;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (
            HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
        )
        {
            return await next(context);
        }

        var refusal = Refusal(request);
        if (refusal != null)
        {
            return Results.Json(new { error = refusal }, statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    string? Refusal(HttpRequest request)
    {
        if (!request.Headers.ContainsKey(REQUEST_HEADER))
        {
            return "missing-request-header";
        }

        if (
            string.Equals(
                request.Headers["Sec-Fetch-Site"].ToString(),
                "cross-site",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return "cross-site";
        }

        var origin = request.Headers.Origin.ToString();
        if (
            origin.Length > 0
            && !string.Equals(
                origin.TrimEnd('/'),
                _settings.Value.AppOrigin.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return "foreign-origin";
        }

        return null;
    }
}
