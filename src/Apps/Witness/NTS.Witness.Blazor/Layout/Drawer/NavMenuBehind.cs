using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Blazor.Components.SelectEvents;
using NTS.Witness.Blazor.Features;
using NTS.Witness.Contracts.Features.Access;
using NTS.Witness.Contracts.Features.Profile;
using static NTS.Witness.Blazor.Routes;

namespace NTS.Witness.Blazor.Layout.Drawer;

public class NavMenuBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    IWitnessProfileContext ProfileContext { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected bool ShowSnapshots => WitnessAccessPolicy.CanViewSnapshots(AccessState.AccessLevel);
    protected bool ShowProfileHeader => ProfileContext.User != null;
    protected string WelcomeName => ProfileContext.WelcomeName;

    protected override async Task OnInitializedAsync()
    {
        await Observe(ProfileContext);
        await Observe(AccessState);
    }

    protected void OpenProfile()
    {
        Navigator.NavigateTo(PROFILE_PAGE);
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
