using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Store;

namespace NC.Server.Authentication.Requests;

/// <summary>
/// Guards the state-changing requests of the sign-in routes against cross-site requests. A request must carry a custom
/// header, which a browser sends cross-origin only after a preflight, and it must not be marked cross-site or come from
/// an origin other than the app's. SameSite alone does not stop a sibling subdomain, which counts as same-site.
/// </summary>
public sealed class StateChangeGuard : IAsyncActionFilter
{
    public const string REQUEST_HEADER = "X-NTS-Request";

    readonly IOptions<AuthenticationSettings> _settings;

    public StateChangeGuard(IOptions<AuthenticationSettings> settings)
    {
        _settings = settings;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (
            HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
        )
        {
            await next();
            return;
        }

        var refusal = Refusal(request);
        if (refusal != null)
        {
            context.Result = new ObjectResult(new { error = refusal }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
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
