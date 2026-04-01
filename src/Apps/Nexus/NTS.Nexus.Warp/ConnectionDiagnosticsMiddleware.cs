using System.Diagnostics;

namespace NTS.Nexus.Warp;

internal sealed class ConnectionDiagnosticsMiddleware
{
    readonly RequestDelegate _next;
    readonly ILogger<ConnectionDiagnosticsMiddleware> _logger;

    public ConnectionDiagnosticsMiddleware(
        RequestDelegate next,
        ILogger<ConnectionDiagnosticsMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!WarpConnectionDiagnostics.TryDescribeTransportRequest(context, out var requestKind, out var hubPath))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Warp transport request {RequestKind} for {HubPath} completed with {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, Method {Method}, Origin {Origin}, Upgrade {Upgrade}, ForwardedProto {ForwardedProto}, ForwardedHost {ForwardedHost}, ArrLogId {ArrLogId}, InstanceId {InstanceId}.",
                requestKind,
                hubPath,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                WarpConnectionDiagnostics.GetCorrelationId(context),
                WarpConnectionDiagnostics.GetConnectionGroup(context),
                WarpConnectionDiagnostics.GetClientName(context),
                WarpConnectionDiagnostics.GetClientVersion(context),
                context.Request.Method,
                context.Request.Headers.Origin.ToString(),
                context.Request.Headers.Upgrade.ToString(),
                WarpConnectionDiagnostics.GetForwardedProto(context),
                WarpConnectionDiagnostics.GetForwardedHost(context),
                context.Request.Headers["X-ARR-LOG-ID"].ToString(),
                WarpConnectionDiagnostics.GetInstanceId()
            );
        }
    }
}
