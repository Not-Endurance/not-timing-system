using System.Diagnostics;
using NC.Blazor.Dialogs.Abstractions;

namespace NC.Blazor.Dialogs;

public class UnhandledExceptionDialogBehind : NDialog
{
    protected string Details => Exception.Demystify().ToString();

    [Parameter, EditorRequired]
    public Exception Exception { get; set; } = default!;
}
