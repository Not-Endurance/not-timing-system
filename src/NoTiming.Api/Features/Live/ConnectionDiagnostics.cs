using Not.Application.RPC;
using NTS.Contracts;

namespace NoTiming.Api.Features.Live;

internal static class ConnectionDiagnostics
{
    const string UNKNOWN_INSTANCE = "local";

    public static bool TryDescribeTransportRequest(HttpContext context, out string requestKind)
    {
        requestKind = string.Empty;

        if (!context.Request.Path.StartsWithSegments(HubPath, out var remainingPath))
        {
            return false;
        }

        if (remainingPath.Equals("/negotiate", StringComparison.OrdinalIgnoreCase))
        {
            requestKind = "negotiate";
            return true;
        }

        if (IsWebSocketUpgrade(context.Request))
        {
            requestKind = "websocket";
            return true;
        }

        return false;
    }

    public static string? GetCorrelationId(HttpContext? context)
    {
        return GetQueryValue(context, RpcConstants.CONNECTION_CORRELATION_ID_KEY);
    }

    public static string? GetConnectionGroup(HttpContext? context)
    {
        return GetQueryValue(context, RpcConstants.CONNECTION_GROUP_KEY);
    }

    public static string? GetClientName(HttpContext? context)
    {
        return GetQueryValue(context, RpcConstants.CONNECTION_CLIENT_NAME_KEY);
    }

    public static string? GetClientVersion(HttpContext? context)
    {
        return GetQueryValue(context, RpcConstants.CONNECTION_CLIENT_VERSION_KEY);
    }

    public static string GetInstanceId()
    {
        return Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? UNKNOWN_INSTANCE;
    }

    public static string? GetForwardedProto(HttpContext context)
    {
        return GetHeader(context, "X-Forwarded-Proto") ?? GetHeader(context, "X-Original-Proto");
    }

    public static string? GetForwardedHost(HttpContext context)
    {
        return GetHeader(context, "X-Forwarded-Host") ?? GetHeader(context, "X-Original-Host");
    }

    public static bool IsWebSocketUpgrade(HttpRequest request)
    {
        // The WebSocket feature is only populated inside the hub's own endpoint pipeline, so read the headers.
        return HttpMethods.IsGet(request.Method)
            && request.Headers.Upgrade.ToString().Contains("websocket", StringComparison.OrdinalIgnoreCase);
    }

    public static PathString HubPath { get; } = new($"/{ApplicationConstants.LIVE_HUB}");

    static string? GetQueryValue(HttpContext? context, string key)
    {
        var value = context?.Request.Query[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    static string? GetHeader(HttpContext context, string headerName)
    {
        var value = context.Request.Headers[headerName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
