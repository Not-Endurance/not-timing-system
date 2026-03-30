using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Blazor.Components.SelectEvents;
using NTS.Witness.Blazor.Features;
using NTS.Witness.Features.Access;

namespace NTS.Witness.Blazor.Layout.Drawer;

public class NavMenuBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    protected bool ShowSnapshots => WitnessAccessPolicy.CanViewSnapshots(AccessState.AccessLevel);

    protected override async Task OnInitializedAsync()
    {
        await Observe(AccessState);
    }

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
