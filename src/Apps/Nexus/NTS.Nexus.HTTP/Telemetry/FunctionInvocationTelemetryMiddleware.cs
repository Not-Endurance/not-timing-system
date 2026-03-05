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
        var (className, methodName) = Resolve(context.FunctionDefinition);
        Activity.Current
            .Tag("faas.name", context.FunctionDefinition.Name)
            .Tag("faas.invocation_id", context.InvocationId)
            .Tag("code.namespace", className)
            .Tag("code.function", methodName);

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            _logger.LogError(
                ex,
                "Unhandled exception in function {FunctionName}",
                context.FunctionDefinition.Name
            );
            throw;
        }
    }

    static (string className, string methodName) Resolve(FunctionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.EntryPoint))
        {
            return ("Function", definition.Name);
        }

        var entryPoint = definition.EntryPoint;
        var methodSeparator = entryPoint.LastIndexOf('.');
        if (methodSeparator < 0)
        {
            return ("Function", entryPoint);
        }

        var methodName = entryPoint[(methodSeparator + 1)..];
        var classPath = entryPoint[..methodSeparator];
        var classSeparator = classPath.LastIndexOf('.');
        var className = classSeparator < 0 ? classPath : classPath[(classSeparator + 1)..];

        return (className, methodName);
    }
}
