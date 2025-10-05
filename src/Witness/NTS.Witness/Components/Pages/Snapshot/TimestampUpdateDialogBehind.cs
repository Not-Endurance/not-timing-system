using System.Text.RegularExpressions;
using MudBlazor;
using NTS.Blazor.Constants;

namespace NTS.Witness.Components.Pages.Snapshot;

public class TimestampUpdateDialogBehind : ComponentBase
{
    protected static readonly PatternMask TIME_MASK = new("00:00:00");

    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public TimestampUpdateModel Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }

    protected void OnTimestampInputChanged(string newValue)
    {
        // Only update if valid; avoid partial/incomplete updates
        if (Regex.IsMatch(newValue, @"^\d{2}:\d{2}:\d{2}$"))
        {
            Model.TimestampInput = newValue;
        }
    }

    protected void Submit()
    {
        MudDialog.Close(DialogResult.Ok(Model));
    }

    protected void Cancel()
    {
        MudDialog.Cancel();
    }
}
