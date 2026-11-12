using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NC.Serialization.JSON;
using NC.Structures;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Shared;

namespace NoTiming.Api.Base;

[Route("api")]
public abstract class ApiControllerBase : ControllerBase
{
    readonly IFunctionLogger<ApiControllerBase> _logger;
    readonly ITelemetryService _telemetry;

    protected ApiControllerBase(IFunctionLogger<ApiControllerBase> logger, ITelemetryService telemetry)
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
        var payload = requestBody.FromJson<TPayload>();
        StampDomainVersion(payload);
        return payload;
    }

    protected new IActionResult Ok()
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

    // A payload is written with the domain version this API was built with, whatever version the client sent.
    // Nested documents are stamped too, because they are stored inside their parent.
    static void StampDomainVersion(object? payload)
    {
        switch (payload)
        {
            case null
            or string:
                return;
            case PersistedModel persisted:
                persisted.DomainVersion = PersistedModel.CURRENT_DOMAIN_VERSION;
                foreach (var property in persisted.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length == 0)
                    {
                        StampDomainVersion(property.GetValue(persisted));
                    }
                }
                return;
            case System.Collections.IEnumerable items:
                foreach (var item in items)
                {
                    StampDomainVersion(item);
                }
                return;
        }
    }
}
