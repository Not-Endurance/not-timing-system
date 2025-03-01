using Microsoft.AspNetCore.Http;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions;

public abstract class FunctionsBase<T>
    where T : FunctionsBase<T>
{
    readonly IFunctionLogger<T> _logger;

    protected FunctionsBase(IFunctionLogger<T> logger)
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
}
