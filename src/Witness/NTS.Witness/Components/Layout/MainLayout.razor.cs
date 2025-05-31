using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Navigation;
using NTS.Witness.Constants;
using SwipeDirection = MudBlazor.SwipeDirection;

namespace NTS.Witness.Components.Layout;

public partial class MainLayout
{
    bool _drawerOpen = false;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    public bool IsCurrentPageEmergencyContacts => Navigator.CurrentEndpoint.Contains(Endpoints.EMERGENCY_CONTACTS);

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
        Navigator.NavigateTo(Endpoints.EMERGENCY_CONTACTS);
    }
}
