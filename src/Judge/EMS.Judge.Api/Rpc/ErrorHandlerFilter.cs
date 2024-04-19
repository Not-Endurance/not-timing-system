using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc;

public class ErrorHandlerFilter : IHubFilter
{
    private readonly ILogger logger;
    
    public ErrorHandlerFilter(ILogger logger)
    {
        this.logger = logger;
    }
    
    public async ValueTask<object> InvokeMethodAsync(
        HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object>> next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception);
            throw;
        }
    }
}
