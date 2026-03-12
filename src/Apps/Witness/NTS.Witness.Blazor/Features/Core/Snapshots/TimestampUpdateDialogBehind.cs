using System.Text.RegularExpressions;
using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor.Features.Core.Snapshots;

public class TimestampUpdateDialogBehind : NComponent
{
    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    protected static PatternMask TIME_MASK { get; set; } = new("00:00:00");

    [Parameter]
    public TimestampUpdateModel Model { get; set; } = default!;

    protected void OnTimestampInputChanged(string newValue)
    {
        try
        {
            if (Regex.IsMatch(newValue, @"^\d{2}:\d{2}:\d{2}$"))
            {
                Model.TimestampInput = newValue;
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
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
            Handle(ex);
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
            Handle(ex);
        }
    }
}
