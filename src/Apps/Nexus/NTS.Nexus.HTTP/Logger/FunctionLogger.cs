using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Not.Injection;

namespace NTS.Nexus.HTTP.Logger;

internal class FunctionLogger<T> : IFunctionLogger<T>
{
    readonly ILogger<T> _logger;
    readonly string _defaultTemplate = "C# HTTP '{function}' processing a {request}";

    public FunctionLogger(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void LogDebug(string template, HttpRequest request, object[] args, [CallerMemberName] string method = "")
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

    public void LogError(string template, HttpRequest request, object[] args, [CallerMemberName] string method = "")
    {
        _logger.LogError(template, [method, request, .. args]);
    }

    public void LogError(HttpRequest request, Exception exception, [CallerMemberName] string method = "")
    {
        _logger.LogError(exception, _defaultTemplate, method, request);
    }

    public void LogDebug(HttpRequest request, [CallerMemberName] string method = "")
    {
        _logger.LogDebug(_defaultTemplate, method, request);
    }

    public void LogInformation(HttpRequest request, [CallerMemberName] string method = "")
    {
        _logger.LogInformation(_defaultTemplate, method, request);
    }

    public void LogError(HttpRequest request, [CallerMemberName] string method = "")
    {
        _logger.LogError(_defaultTemplate, method, request);
    }
}

public interface IFunctionLogger<out T> : ITransient
{
    void LogDebug(HttpRequest request, [CallerMemberName] string method = "");
    void LogDebug(string template, HttpRequest request, object[] args, [CallerMemberName] string method = "");
    void LogInformation(HttpRequest request, [CallerMemberName] string method = "");
    void LogInformation(string template, HttpRequest request, object[] args, [CallerMemberName] string method = "");
    void LogError(HttpRequest request, [CallerMemberName] string method = "");
    void LogError(string template, HttpRequest request, object[] args, [CallerMemberName] string method = "");
    void LogError(HttpRequest request, Exception exception, [CallerMemberName] string method = "");
}
