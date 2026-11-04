using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Not.Exceptions;
using Not.Structures;
using NoTiming.Api.Telemetry;

namespace NoTiming.Api.Errors;

public sealed class ApiExceptionFilter : IExceptionFilter
{
    readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ValidationException validation)
        {
            Activity
                .Current.Tag("exception.handled", true)
                .Tag("validation.property", validation.Property)
                .TagException(validation);

            context.Result = new OkObjectResult(Result.Failure(validation.Message));
            context.ExceptionHandled = true;
            return;
        }

        Activity.Current.TagException(context.Exception);
        _logger.LogError(context.Exception, "Unhandled exception in {Action}", context.ActionDescriptor.DisplayName);
    }
}
