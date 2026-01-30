using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Serialization.JSON;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions.Base;

public abstract class FunctionBase
{
    readonly IFunctionLogger<FunctionBase> _logger;

    protected FunctionBase(IFunctionLogger<FunctionBase> logger)
    {
        _logger = logger;
    }

    protected void LogInformation(HttpRequest request)
    {
        _logger.LogInformation(request);
    }

    protected void LogError(HttpRequest request)
    {
        _logger.LogError(request);
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
}
