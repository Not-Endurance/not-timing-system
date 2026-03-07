using MudBlazor;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using SwipeDirection = MudBlazor.SwipeDirection;

namespace NTS.Witness.Blazor.Components.Shared;

public class MainLayoutBehind : LayoutComponentBase
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    protected bool DrawerOpen { get; set; } = false;

    public bool IsCurrentPageEmergencyContacts =>
        Navigator.CurrentEndpoint.Contains(WitnessBlazorConstants.Pages.EMERGENCY_CONTACTS);

    protected void ToggleDrawer()
    {
        DrawerOpen = !DrawerOpen;
    }

    protected void CloseDrawer()
    {
        DrawerOpen = false;
    }

    protected void HelpHandler()
    {
        Navigator.NavigateTo(WitnessBlazorConstants.Pages.EMERGENCY_CONTACTS);
    }

    protected Task Signout()
    {
        Authentication.Signout();
        return Task.CompletedTask;
    }

    public void OnSwipeEnd(SwipeEventArgs e)
    {
        if (e.SwipeDirection == SwipeDirection.LeftToRight && DrawerOpen)
        {
            DrawerOpen = false;
            StateHasChanged();
        }
        else if (e.SwipeDirection == SwipeDirection.RightToLeft && !DrawerOpen)
        {
            DrawerOpen = true;
            StateHasChanged();
        }
    }
}
