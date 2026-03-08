using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using NTS.Application.Cors;

namespace NTS.Nexus.HTTP.Cors;

internal sealed class FunctionsCorsMiddleware : IFunctionsWorkerMiddleware
{
    static readonly string[] DEFAULT_METHODS = ["GET", "POST", "PATCH", "DELETE", "OPTIONS"];

    readonly ICorsOriginValidator _originValidator;

    public FunctionsCorsMiddleware(ICorsOriginValidator originValidator)
    {
        _originValidator = originValidator;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            await next(context);
            return;
        }

        var origin = httpContext.Request.Headers.Origin.ToString();
        var isAllowed = _originValidator.IsAllowed(origin);

        if (HttpMethods.IsOptions(httpContext.Request.Method))
        {
            if (!isAllowed)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            ApplyOriginHeaders(httpContext.Response.Headers, origin);
            ApplyPreflightHeaders(httpContext);
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);

        if (isAllowed)
        {
            ApplyOriginHeaders(httpContext.Response.Headers, origin);
        }
    }

    static void ApplyOriginHeaders(IHeaderDictionary headers, string origin)
    {
        headers["Access-Control-Allow-Origin"] = origin;
        headers["Access-Control-Allow-Credentials"] = "true";
        headers["Vary"] = "Origin";
    }

    static void ApplyPreflightHeaders(HttpContext context)
    {
        var requestedHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();
        context.Response.Headers["Access-Control-Allow-Headers"] = string.IsNullOrWhiteSpace(requestedHeaders)
            ? "Content-Type,Authorization"
            : requestedHeaders;

        var requestedMethod = context.Request.Headers["Access-Control-Request-Method"].ToString();
        context.Response.Headers["Access-Control-Allow-Methods"] = string.IsNullOrWhiteSpace(requestedMethod)
            ? string.Join(",", DEFAULT_METHODS)
            : requestedMethod;
    }
}
