using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Blazor.Components.SelectEvents;

namespace NTS.Witness.Blazor.Layout.Drawer;

public class NavMenuBehind : NComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected async Task OpenSelectEventDialog()
    {
        try
        {
            var dialog = await DialogService.ShowAsync<SelectEventDialog>(Select_event_string);
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
