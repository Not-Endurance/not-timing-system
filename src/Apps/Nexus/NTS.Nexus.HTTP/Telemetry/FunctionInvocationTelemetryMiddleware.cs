using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace NTS.Nexus.HTTP.Telemetry;

internal class FunctionInvocationTelemetryMiddleware : IFunctionsWorkerMiddleware
{
    readonly ILogger<FunctionInvocationTelemetryMiddleware> _logger;

    public FunctionInvocationTelemetryMiddleware(ILogger<FunctionInvocationTelemetryMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            _logger.LogError(ex, "Unhandled exception in function {FunctionName}", context.FunctionDefinition.Name);
            throw;
        }
    }
}
