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

    protected FunctionBase(IFunctionLogger<FunctionBase> logger)
    {
        _logger = logger;
    }

    protected void TagRequest(HttpRequest request)
    {
        Activity.Current
            .Tag("http.request.method", request.Method)
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

    protected async Task<TPayload?> ReadBody<TPayload>(HttpRequest request)
        where TPayload : class
    {
        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        return requestBody.TryFromJson<TPayload>();
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
