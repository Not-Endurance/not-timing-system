using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Mud;
using Not.Blazor.Navigation;
using NTS.Witness.Web.Constants;
using static NTS.Witness.Web.Constants.Endpoints;
using SwipeDirection = MudBlazor.SwipeDirection;

namespace NTS.Witness.Web.Components.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
    protected bool DrawerOpen { get; set; } = false;

    public bool IsCurrentPageEmergencyContacts => Navigator.CurrentEndpoint.Contains(EMERGENCY_CONTACTS);

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
        Navigator.NavigateTo(EMERGENCY_CONTACTS);
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
