using Not.Application.RPC;
using NTS.Application;

namespace NTS.Nexus.Warp;

internal static class WarpConnectionDiagnostics
{
    static readonly PathString JudgeHubPath = new($"/{ApplicationConstants.JUDGE_HUB}");
    static readonly PathString WitnessHubPath = new($"/{ApplicationConstants.WITNESS_HUB}");
    const string UnknownInstance = "local";

    public static bool TryDescribeTransportRequest(HttpContext context, out string requestKind, out string hubPath)
    {
        requestKind = string.Empty;
        hubPath = string.Empty;

        if (!TryResolveHubPath(context.Request.Path, out var resolvedHubPath, out var remainingPath))
        {
            return false;
        }

        if (remainingPath.Equals("/negotiate", StringComparison.OrdinalIgnoreCase))
        {
            requestKind = "negotiate";
            hubPath = resolvedHubPath;
            return true;
        }

        if (context.WebSockets.IsWebSocketRequest)
        {
            requestKind = "websocket";
            hubPath = resolvedHubPath;
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
        return Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? UnknownInstance;
    }

    public static string? GetForwardedProto(HttpContext context)
    {
        return GetHeader(context, "X-Forwarded-Proto") ?? GetHeader(context, "X-Original-Proto");
    }

    public static string? GetForwardedHost(HttpContext context)
    {
        return GetHeader(context, "X-Forwarded-Host") ?? GetHeader(context, "X-Original-Host");
    }

    static bool TryResolveHubPath(PathString requestPath, out string hubPath, out string remainingPath)
    {
        hubPath = string.Empty;
        remainingPath = string.Empty;

        if (requestPath.StartsWithSegments(JudgeHubPath, out var judgeRemainingPath))
        {
            hubPath = JudgeHubPath.Value!;
            remainingPath = judgeRemainingPath.Value ?? string.Empty;
            return true;
        }

        if (requestPath.StartsWithSegments(WitnessHubPath, out var witnessRemainingPath))
        {
            hubPath = WitnessHubPath.Value!;
            remainingPath = witnessRemainingPath.Value ?? string.Empty;
            return true;
        }

        return false;
    }

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
