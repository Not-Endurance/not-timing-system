using MudBlazor;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Socket;
using NTS.Blazor.Components.SelectEvents;
using NoTiming.Ui.Blazor.Features;
using NoTiming.Ui.Contracts.Features.Access;
using NoTiming.Ui.Contracts.Features.Profile;

namespace NoTiming.Ui.Blazor.Layout.Drawer;

public class NavMenuBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    [Inject]
    IUiAccessContext AccessState { get; set; } = default!;

    [Inject]
    IUiProfileContext ProfileContext { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [CascadingParameter(Name = "CloseResponsiveDrawer")]
    Func<Task>? CloseResponsiveDrawer { get; set; }

    protected bool ShowSnapshots => UiAccessPolicy.CanViewSnapshots(AccessState.AccessLevel);
    protected bool ShowProfileHeader => ProfileContext.User != null;
    protected bool HasActiveEvent => SocketService.IsConnected && SocketService.Event != null;
    protected string ActiveEventTitle => SocketService.Event?.Name ?? Event_string;
    protected string WelcomeName => ProfileContext.WelcomeName;

    protected override async Task OnInitializedAsync()
    {
        await Observe(ProfileContext);
        await Observe(AccessState);
        await Observe(SocketService);
    }

    protected async Task Signout()
    {
        try
        {
            await Authentication.Signout();
            await SocketService.Disconnect();
            await CloseResponsiveDrawerSafe();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenSelectEventDialog()
    {
        try
        {
            await CloseResponsiveDrawerSafe();
            var dialog = await DialogService.ShowAsync<SelectEventDialog>(Select_event_string);
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task CloseResponsiveDrawerSafe()
    {
        if (CloseResponsiveDrawer != null)
        {
            await CloseResponsiveDrawer();
        }
    }
}
