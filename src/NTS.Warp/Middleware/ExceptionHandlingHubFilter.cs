using Microsoft.AspNetCore.SignalR;
using Not.Logging;
using Not.Notify;

namespace NTS.Warp.Middleware;

internal class ExceptionHandlingHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        Console.WriteLine($"Calling hub method '{invocationContext.HubMethodName}'");
        try
        {
            return await next(invocationContext);
        }
        catch (HubException ex)
        {
            HandleHubException(ex, invocationContext.HubMethodName);
            return Task.FromException(ex);
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try
        {
            await next(context);
        }
        catch (HubException ex)
        {
            HandleHubException(ex, nameof(OnConnectedAsync));
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
        catch (HubException ex)
        {
            HandleHubException(ex, nameof(OnDisconnectedAsync));
        }
    }

    public void HandleHubException(HubException hubException, string methodName)
    {
        NotifyHelper.Error(hubException);
        var logMessage =
            $"An error {hubException.Message} was thrown calling {methodName} "
            + $"at {hubException.Source} with trace \n {hubException.StackTrace}";
        LoggingHelper.Error(logMessage);
    }
}
