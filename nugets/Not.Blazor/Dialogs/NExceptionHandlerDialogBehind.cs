using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Not.Application.Environments;
using Not.Blazor.Dialogs.Abstractions;

namespace Not.Blazor.Dialogs;

public class NExceptionHandlerDialogBehind : NDialog
{
    bool _exceptionLogged;

    [Inject]
    internal IEnvironmentContext EnvironmentContext { get; set; } = default!;

    [Inject]
    internal ILogger<NExceptionHandlerDialogBehind> Logger { get; set; } = default!;

    protected string Details => Exception.Demystify().ToString();
    protected bool IsProduction => EnvironmentContext.IsProduction();

    [Parameter, EditorRequired]
    public Exception Exception { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_exceptionLogged)
        {
            return;
        }

        Logger.LogError(Exception, "Unhandled exception displayed by Blazor exception dialog.");
        _exceptionLogged = true;
    }
}
