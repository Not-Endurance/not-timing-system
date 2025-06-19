using System.Text.RegularExpressions;
using MudBlazor;
using NTS.Blazor.Constants;

namespace NTS.Witness.Components.Pages.Snapshot;

public partial class TimestampUpdateDialog
{
    static readonly PatternMask TIME_MASK = new("00:00:00");

    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public TimestampUpdateModel Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }

    void OnTimestampInputChanged(string newValue)
    {
        // Only update if valid; avoid partial/incomplete updates
        if (Regex.IsMatch(newValue, @"^\d{2}:\d{2}:\d{2}$"))
        {
            Model.TimestampInput = newValue;
        }
    }

    void Submit()
    {
        MudDialog.Close(DialogResult.Ok(Model));
    }

    void Cancel()
    {
         MudDialog.Cancel();
    }
}
