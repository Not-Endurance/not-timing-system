using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Not.Blazor.Components.Exceptions;

public class NExceptionHandlerErrorBoundary : ErrorBoundary
{
    [Inject]
    internal ILogger<NExceptionHandlerErrorBoundary> Logger { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Unhandled exception caught by Blazor error boundary.");
        return Task.CompletedTask;
    }
}
