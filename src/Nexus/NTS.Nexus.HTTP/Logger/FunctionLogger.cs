using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NTS.Nexus.HTTP.Logger;

internal class FunctionLogger<T> : IFunctionLogger<T>
{
    readonly ILogger<T> _logger;
    readonly string _defaultTemplate = "C# HTTP '{function}' processing a {request}";

    public FunctionLogger(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void LogDebug(
        string template,
        HttpRequest request,
        object[] args,
        [CallerMemberName] string method = ""
    )
    {
        _logger.LogDebug(template, [method, request, .. args]);
    }

    public void LogInformation(
        string template,
        HttpRequest request,
        object[] args,
        [CallerMemberName] string method = ""
    )
    {
        _logger.LogInformation(template, [method, request, .. args]);
    }

    public void LogError(
        string template,
        HttpRequest request,
        object[] args,
        [CallerMemberName] string method = ""
    )
    {
        _logger.LogError(template, [method, request, .. args]);
    }

    public void LogDebug(HttpRequest request, [CallerMemberName] string method = "")
    {
        _logger.LogDebug(_defaultTemplate, request, method);
    }

    public void LogInformation(HttpRequest request, [CallerMemberName] string method = "")
    {
        _logger.LogInformation(_defaultTemplate, request, method);
    }

    public void LogError(HttpRequest request, [CallerMemberName] string method = "")
    {
        _logger.LogError(_defaultTemplate, request, method);
    }
}

public interface IFunctionLogger<T>
{
    void LogDebug(HttpRequest request, [CallerMemberName] string method = "");
    void LogDebug(
        string template,
        HttpRequest request,
        object[] args,
        [CallerMemberName] string method = ""
    );
    void LogInformation(HttpRequest request, [CallerMemberName] string method = "");
    void LogInformation(
        string template,
        HttpRequest request,
        object[] args,
        [CallerMemberName] string method = ""
    );
    void LogError(HttpRequest request, [CallerMemberName] string method = "");
    void LogError(
        string template,
        HttpRequest request,
        object[] args,
        [CallerMemberName] string method = ""
    );
}
