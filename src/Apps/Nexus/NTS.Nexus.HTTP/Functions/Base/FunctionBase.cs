using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Serialization.JSON;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Base;

public abstract class FunctionBase
{
    readonly IFunctionLogger<FunctionBase> _logger;
    readonly ITelemetryService _telemetry;

    protected FunctionBase(IFunctionLogger<FunctionBase> logger, ITelemetryService telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    protected Task<IActionResult> ExecuteHttp(
        HttpRequest request,
        string methodName,
        Func<Task<IActionResult>> action
    )
    {
        return ExecuteWithTelemetry(methodName, async () =>
        {
            Activity.Current?.SetTag("http.request.method", request.Method);
            Activity.Current?.SetTag("url.path", request.Path.ToString());
            Activity.Current?.SetTag("url.query", request.QueryString.ToString());

            LogInformation(request, methodName);
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                ex.AttachToCurrentActivity();
                _logger.LogError(request, ex, methodName);
                throw;
            }
        });
    }

    protected Task<TResult> ExecuteWithTelemetry<TResult>(string methodName, Func<Task<TResult>> action)
    {
        return ExecuteWithTelemetry(GetType().Name, methodName, action);
    }

    protected Task ExecuteWithTelemetry(string methodName, Func<Task> action)
    {
        return ExecuteWithTelemetry(
            methodName,
            async () =>
            {
                await action();
                return true;
            }
        );
    }

    protected async Task<TResult> ExecuteWithTelemetry<TResult>(
        string className,
        string methodName,
        Func<Task<TResult>> action
    )
    {
        using var activity = _telemetry.StartActivity(className, methodName);

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            ex.AttachToCurrentActivity();
            throw;
        }
    }

    protected void LogInformation(HttpRequest request)
    {
        _logger.LogInformation(request);
    }

    protected void LogInformation(HttpRequest request, string methodName)
    {
        _logger.LogInformation(request, methodName);
    }

    protected void LogError(HttpRequest request)
    {
        _logger.LogError(request);
    }

    protected Task<TPayload?> ReadBody<TPayload>(HttpRequest request)
        where TPayload : class
    {
        return ExecuteWithTelemetry(nameof(ReadBody), async () =>
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            return requestBody.TryFromJson<TPayload>();
        });
    }

    protected IActionResult Ok()
    {
        return new OkResult();
    }

    protected IActionResult Ok<TPayload>(TPayload payload)
    {
        return new OkObjectResult(payload);
    }

    protected IActionResult UnexpectedPayload<TPayload>()
    {
        return new BadRequestObjectResult($"Payload couldn't be parsed to '{typeof(TPayload).FullName}'");
    }

    protected IActionResult InvalidPayload(string message)
    {
        return new BadRequestObjectResult(message);
    }

    protected IActionResult NotFound(object? criteria)
    {
        return new NotFoundObjectResult($"Entity with '{criteria}' not found");
    }
}
