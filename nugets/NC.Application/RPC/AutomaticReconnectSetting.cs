using Microsoft.AspNetCore.SignalR.Client;

namespace Not.Application.RPC;

public class AutomaticReconnectSetting : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.PreviousRetryCount < 3)
        {
            return TimeSpan.FromSeconds(1);
        }
        else
        {
            return TimeSpan.FromSeconds(8);
        }
    }
}
