using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Not.Exceptions;
using Not.Structures;

namespace NTS.Nexus.HTTP.Telemetry;

internal class ErrorHandlerMiddleware : IFunctionsWorkerMiddleware
{
    readonly ILogger<ErrorHandlerMiddleware> _logger;

    public ErrorHandlerMiddleware(ILogger<ErrorHandlerMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex) when (TryHandleValidationFailure(context, ex)) { }
        catch (Exception ex)
        {
            Activity.Current?.TagException(ex);
            _logger.LogError(ex, "Unhandled exception in function {FunctionName}", context.FunctionDefinition.Name);
            throw;
        }
    }

    bool TryHandleValidationFailure(FunctionContext context, ValidationException exception)
    {
        if (context.GetHttpContext() == null)
        {
            return false;
        }

        Activity
            .Current.Tag("exception.handled", true)
            .Tag("validation.property", exception.Property)
            .TagException(exception);

        context.GetInvocationResult().Value = new OkObjectResult(Result.Failure(exception.Message));
        return true;
    }
}
