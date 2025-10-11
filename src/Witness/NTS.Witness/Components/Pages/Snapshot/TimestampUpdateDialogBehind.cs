using System.Text.RegularExpressions;
using MudBlazor;
using Not.Safe;
using NTS.Blazor.Constants;

namespace NTS.Witness.Components.Pages.Snapshot;

public class TimestampUpdateDialogBehind : ComponentBase
{
    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    protected static PatternMask TIME_MASK { get; set; } = new("00:00:00");

    [Parameter]
    public TimestampUpdateModel Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }

    protected void OnTimestampInputChanged(string newValue)
    {
        try
        {
            // Only update if valid; avoid partial/incomplete updates
            if (Regex.IsMatch(newValue, @"^\d{2}:\d{2}:\d{2}$"))
            {
                Model.TimestampInput = newValue;
            }
        }
        catch (Exception ex)
        {
            SafeHelper.HandleError(ex);
        }
    }

    protected void Submit()
    {
        try
        {
            MudDialog.Close(DialogResult.Ok(Model));

        }
        catch (Exception ex)
        {
            SafeHelper.HandleError(ex);
        }
    }

    protected void Cancel()
    {
        try
        {
            MudDialog.Cancel();
        }
        catch (Exception ex)
        {
            SafeHelper.HandleError(ex);
        }
    }
}
