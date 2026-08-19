using MudBlazor;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Socket;
using NTS.Blazor.Components.SelectEvents;
using NTS.Witness.Blazor.Features;
using NTS.Witness.Contracts.Features.Access;
using NTS.Witness.Contracts.Features.Profile;

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
    INtsSocketService SocketService { get; set; } = default!;

    [CascadingParameter(Name = "CloseResponsiveDrawer")]
    Func<Task>? CloseResponsiveDrawer { get; set; }

    protected bool ShowSnapshots => WitnessAccessPolicy.CanViewSnapshots(AccessState.AccessLevel);
    protected bool ShowSignin => WitnessAccessPolicy.CanSignIn(AccessState.AccessLevel);
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

    protected async Task Signin()
    {
        try
        {
            await CloseResponsiveDrawerSafe();
            await Authentication.Signin();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>
    /// Dropping the socket before signing out both closes the officiating connection while this
    /// page is still alive and recomputes the access level, which keeps the drawer honest on the
    /// paths where the logout resolves client-side instead of redirecting. Reconnecting is not
    /// this component's job: a logout that does redirect reloads the app, and
    /// <c>EventConnectionCoordinator</c> connects anonymously from there.
    /// </summary>
    protected async Task Signout()
    {
        try
        {
            await CloseResponsiveDrawerSafe();
            await SocketService.Disconnect();
            await Authentication.Signout();
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
