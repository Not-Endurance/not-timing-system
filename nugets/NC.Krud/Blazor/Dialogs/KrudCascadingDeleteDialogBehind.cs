using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs.Abstractions;
using Not.Krud.Models;

namespace Not.Krud.Blazor.Dialogs;

public class KrudCascadingDeleteDialogBehind : NDialog
{
    [Parameter]
    public KrudDeleteImpact Impact { get; set; } = new(string.Empty, []);
}
