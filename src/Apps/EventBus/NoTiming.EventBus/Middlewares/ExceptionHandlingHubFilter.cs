using Microsoft.AspNetCore.SignalR;
using Not.Notify;
using NoTiming.EventBus.ConnectionDiagnostics;

namespace NoTiming.EventBus.Middlewares;

internal class ExceptionHandlingHubFilter : IHubFilter
{
    readonly INotifier _notifier;
    readonly ILogger<ExceptionHandlingHubFilter> _logger;

    public ExceptionHandlingHubFilter(INotifier notifier, ILogger<ExceptionHandlingHubFilter> logger)
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

    void HandleHubException(Exception exception, string methodName, HubCallerContext context, string hubName)
    {
        _notifier.Error(exception);

        var httpContext = context.GetHttpContext();
        _logger.LogError(
            exception,
            "NoTiming hub exception in {HubName}.{MethodName}. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
            hubName,
            methodName,
            context.ConnectionId,
            NoTimingConnectionDiagnostics.GetCorrelationId(httpContext),
            NoTimingConnectionDiagnostics.GetConnectionGroup(httpContext),
            NoTimingConnectionDiagnostics.GetClientName(httpContext),
            NoTimingConnectionDiagnostics.GetClientVersion(httpContext),
            NoTimingConnectionDiagnostics.GetInstanceId()
        );
    }
}
