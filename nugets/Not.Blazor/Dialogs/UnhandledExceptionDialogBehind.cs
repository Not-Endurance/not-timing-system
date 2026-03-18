using System.Diagnostics;
using Not.Blazor.Dialogs.Abstractions;

namespace Not.Blazor.Dialogs;

public class UnhandledExceptionDialogBehind : NDialog
{
    protected string Details => Exception.Demystify().ToString();

    [Parameter, EditorRequired]
    public Exception Exception { get; set; } = default!;
}
