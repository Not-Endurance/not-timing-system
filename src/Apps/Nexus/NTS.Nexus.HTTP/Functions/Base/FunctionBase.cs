using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Serialization.JSON;
using Not.Structures;
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

    protected Activity? StartFunctionActivity(string methodName)
    {
        return _telemetry.StartActivity(GetType().Name, methodName);
    }

    protected void TagRequest(HttpRequest request)
    {
        Activity
            .Current.Tag("http.request.method", request.Method)
            .Tag("url.path", request.Path.ToString())
            .Tag("url.query", request.QueryString.ToString());
    }

    protected void LogInformation(HttpRequest request, string methodName)
    {
        _logger.LogInformation(request, methodName);
    }

    protected void LogError(HttpRequest request)
    {
        _logger.LogError(request);
    }

    protected void LogError(HttpRequest request, Exception exception, string methodName)
    {
        Activity.Current.TagException(exception);
        _logger.LogError(request, exception, methodName);
    }

    protected async Task<TPayload> ReadBody<TPayload>(HttpRequest request)
        where TPayload : class
    {
        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        return requestBody.FromJson<TPayload>();
    }

    protected IActionResult Ok()
    {
        return new OkObjectResult(Result.Success());
    }

    protected IActionResult Ok<TPayload>(TPayload payload)
    {
        return new OkObjectResult(Result.Success(payload!));
    }

    protected IActionResult Failure(params string[] errors)
    {
        return new OkObjectResult(Result.Failure(errors));
    }

    protected IActionResult InvalidPayload(string message)
    {
        return new BadRequestObjectResult(message);
    }
}
