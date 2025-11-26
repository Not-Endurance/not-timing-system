using MudBlazor;
using Not.Blazor.Navigation;
using SwipeDirection = MudBlazor.SwipeDirection;

namespace NTS.Witness.Blazor.Components.Layout;

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
