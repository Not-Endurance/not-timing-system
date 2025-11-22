using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Mud;
using Not.Blazor.Navigation;
using NTS.Witness.Web.Constants;
using static NTS.Witness.Web.Constants.Endpoints;
using SwipeDirection = MudBlazor.SwipeDirection;

namespace NTS.Witness.Web.Components.Layout;

public partial class MainLayout
{
    bool _drawerOpen = false;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    public bool IsCurrentPageEmergencyContacts => Navigator.CurrentEndpoint.Contains(EMERGENCY_CONTACTS);

    public void OnSwipeEnd(SwipeEventArgs e)
    {
        if (e.SwipeDirection == SwipeDirection.LeftToRight && _drawerOpen)
        {
            _drawerOpen = false;
            StateHasChanged();
        }
        else if (e.SwipeDirection == SwipeDirection.RightToLeft && !_drawerOpen)
        {
            _drawerOpen = true;
            StateHasChanged();
        }
    }

    void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    void CloseDrawer()
    {
        _drawerOpen = false;
    }

    void HelpHandler()
    {
        Navigator.NavigateTo(EMERGENCY_CONTACTS);
    }
}
