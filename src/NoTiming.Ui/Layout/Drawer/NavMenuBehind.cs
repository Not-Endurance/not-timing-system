using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Components.SelectEvents;
using NoTiming.Ui.Features;
using NoTiming.Ui.Features.Account;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Profile;
using NTS.Contracts.Socket;

namespace NoTiming.Ui.Layout.Drawer;

public class NavMenuBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    IAccountSession Account { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

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
    protected bool ShowProfileHeader => Account.IsSignedIn;
    protected bool HasLiveEvent => SocketService.IsConnected && SocketService.Event != null;
    protected string LiveEventTitle => SocketService.Event?.Name ?? Event_string;
    protected string WelcomeName => ProfileContext.WelcomeName;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Account);
        await Observe(ProfileContext);
        await Observe(AccessState);
        await Observe(SocketService);
    }

    /// <summary>The address of a page of the Live Event the app follows: its Event is in the address (#630).</summary>
    protected string LiveEventRoute(string eventPage)
    {
        return Routes.Of(eventPage, SocketService.Event!.Id);
    }

    /// <summary>Signing in is the host's page, which returns the person to the page they were on.</summary>
    protected async Task Signin()
    {
        try
        {
            await CloseResponsiveDrawerSafe();
            Navigator.NavigateTo(
                AccountSession.SignInUrl($"/{Navigator.ToBaseRelativePath(Navigator.Uri)}"),
                forceLoad: true
            );
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>
    /// Ends the session at the host and loads the app again, as a visitor: what the app holds of the person, such as the
    /// Snapshots they had selected, goes with the page and not into whatever is shown next.
    /// </summary>
    protected async Task Signout()
    {
        try
        {
            await CloseResponsiveDrawerSafe();
            await Account.SignOut();
            Navigator.NavigateTo(Routes.HOME, forceLoad: true);
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
