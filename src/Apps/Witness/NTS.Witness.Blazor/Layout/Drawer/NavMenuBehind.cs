using MudBlazor;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Components.Abstractions;
using NTS.Blazor.Components.SelectEvents;
using NTS.Application.Contracts.Socket;
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
    INAuthentication Authentication { get; set; } = default!;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    IWitnessProfileContext ProfileContext { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    protected bool ShowSnapshots => WitnessAccessPolicy.CanViewSnapshots(AccessState.AccessLevel);
    protected bool ShowProfileHeader => ProfileContext.User != null;
    protected bool HasActiveEvent => SocketService.IsConnected && SocketService.Event != null;
    protected string ActiveEventTitle => SocketService.Event?.Name ?? Event_string;
    protected string WelcomeName => ProfileContext.WelcomeName[..12];

    protected override async Task OnInitializedAsync()
    {
        await Observe(ProfileContext);
        await Observe(AccessState);
        await Observe(SocketService);
    }

    protected void OpenProfile()
    {
        Navigator.NavigateTo(PROFILE_PAGE);
    }

    protected async Task Signout()
    {
        await Authentication.Signout();
        await SocketService.Disconnect();
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
