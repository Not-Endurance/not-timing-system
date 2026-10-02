using NTS.Application.Cors;

namespace NoTiming.Api.Features.Live;

/// <summary>
/// Refuses a WebSocket to the hub from an origin that is neither this host nor one the CORS settings allow. A request
/// without an Origin header (a non-browser client) passes: the hub is anonymous and writes nothing.
/// </summary>
internal sealed class HubOriginMiddleware
{
    readonly RequestDelegate _next;
    readonly ICorsOriginValidator _originValidator;
    readonly ILogger<HubOriginMiddleware> _logger;

    public HubOriginMiddleware(
        RequestDelegate next,
        ICorsOriginValidator originValidator,
        ILogger<HubOriginMiddleware> logger
    )
    {
        _next = next;
        _originValidator = originValidator;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (
            ConnectionDiagnostics.IsWebSocketUpgrade(context.Request)
            && context.Request.Path.StartsWithSegments(ConnectionDiagnostics.HubPath)
        )
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrWhiteSpace(origin) && !IsThisHost(origin, context.Request.Host) && !_originValidator.IsAllowed(origin))
            {
                _logger.LogWarning(
                    "Live hub rejected WebSocket request for {Path} because origin {Origin} is not allowed. CorrelationId {CorrelationId}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
                    context.Request.Path,
                    origin,
                    ConnectionDiagnostics.GetCorrelationId(context),
                    ConnectionDiagnostics.GetClientName(context),
                    ConnectionDiagnostics.GetClientVersion(context),
                    ConnectionDiagnostics.GetInstanceId()
                );
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }

    static bool IsThisHost(string origin, HostString host)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && string.Equals(uri.Authority, host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
