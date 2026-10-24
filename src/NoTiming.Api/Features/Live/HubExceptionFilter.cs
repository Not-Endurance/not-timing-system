using Microsoft.AspNetCore.SignalR;

namespace NoTiming.Api.Features.Live;

internal sealed class HubExceptionFilter : IHubFilter
{
    readonly ILogger<HubExceptionFilter> _logger;

    public HubExceptionFilter(ILogger<HubExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Log(ex, nameof(OnConnectedAsync), context.Context, context.Hub.GetType().Name);
            throw;
        }
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next
    )
    {
        try
        {
            await next(context, exception);
        }
        catch (Exception ex)
        {
            Log(ex, nameof(OnDisconnectedAsync), context.Context, context.Hub.GetType().Name);
        }
    }

    void Log(Exception exception, string methodName, HubCallerContext context, string hubName)
    {
        var httpContext = context.GetHttpContext();
        _logger.LogError(
            exception,
            "Hub exception in {HubName}.{MethodName}. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
            hubName,
            methodName,
            context.ConnectionId,
            ConnectionDiagnostics.GetCorrelationId(httpContext),
            ConnectionDiagnostics.GetConnectionGroup(httpContext),
            ConnectionDiagnostics.GetClientName(httpContext),
            ConnectionDiagnostics.GetClientVersion(httpContext),
            ConnectionDiagnostics.GetInstanceId()
        );
    }
}
