using System.Diagnostics;

namespace NoTiming.Api.Features.Live;

internal sealed class ConnectionDiagnosticsMiddleware
{
    readonly RequestDelegate _next;
    readonly ILogger<ConnectionDiagnosticsMiddleware> _logger;

    public ConnectionDiagnosticsMiddleware(RequestDelegate next, ILogger<ConnectionDiagnosticsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ConnectionDiagnostics.TryDescribeTransportRequest(context, out var requestKind))
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
                "Live hub transport request {RequestKind} completed with {StatusCode} in {ElapsedMilliseconds} ms. "
                    + "CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, Method {Method}, "
                    + "Origin {Origin}, Upgrade {Upgrade}, ForwardedProto {ForwardedProto}, "
                    + "ForwardedHost {ForwardedHost}, ArrLogId {ArrLogId}, InstanceId {InstanceId}.",
                requestKind,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                ConnectionDiagnostics.GetCorrelationId(context),
                ConnectionDiagnostics.GetConnectionGroup(context),
                ConnectionDiagnostics.GetClientName(context),
                ConnectionDiagnostics.GetClientVersion(context),
                context.Request.Method,
                context.Request.Headers.Origin.ToString(),
                context.Request.Headers.Upgrade.ToString(),
                ConnectionDiagnostics.GetForwardedProto(context),
                ConnectionDiagnostics.GetForwardedHost(context),
                context.Request.Headers["X-ARR-LOG-ID"].ToString(),
                ConnectionDiagnostics.GetInstanceId()
            );
        }
    }
}
