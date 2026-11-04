using Microsoft.AspNetCore.Components;
using NC.Blazor.Dialogs.Abstractions;
using NC.Krud.Models;

namespace NC.Krud.Blazor.Dialogs;

public class KrudCascadingDeleteDialogBehind : NDialog
{
    [Parameter]
    public KrudDeleteImpact Impact { get; set; } = new(string.Empty, []);
}
