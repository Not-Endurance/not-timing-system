using Microsoft.AspNetCore.SignalR;
using Not.Notify;
using NTS.Nexus.Warp.ConnectionDiagnostics;

namespace NTS.Nexus.Warp.Middlewares;

internal class ExceptionHandlingHubFilter : IHubFilter
{
    readonly INotifier _notifier;
    readonly ILogger<ExceptionHandlingHubFilter> _logger;

    public ExceptionHandlingHubFilter(
        INotifier notifier,
        ILogger<ExceptionHandlingHubFilter> logger
    )
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception ex)
        {
            HandleHubException(
                ex,
                invocationContext.HubMethodName,
                invocationContext.Context,
                invocationContext.Hub.GetType().Name
            );
            throw;
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            HandleHubException(ex, nameof(OnConnectedAsync), context.Context, context.Hub.GetType().Name);
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
            HandleHubException(ex, nameof(OnDisconnectedAsync), context.Context, context.Hub.GetType().Name);
        }
    }

    void HandleHubException(
        Exception exception,
        string methodName,
        HubCallerContext context,
        string hubName
    )
    {
        _notifier.Error(exception);

        var httpContext = context.GetHttpContext();
        _logger.LogError(
            exception,
            "Warp hub exception in {HubName}.{MethodName}. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
            hubName,
            methodName,
            context.ConnectionId,
            WarpConnectionDiagnostics.GetCorrelationId(httpContext),
            WarpConnectionDiagnostics.GetConnectionGroup(httpContext),
            WarpConnectionDiagnostics.GetClientName(httpContext),
            WarpConnectionDiagnostics.GetClientVersion(httpContext),
            WarpConnectionDiagnostics.GetInstanceId()
        );
    }
}
