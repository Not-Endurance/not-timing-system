using Microsoft.AspNetCore.Components;
using Not.Blazor.Navigation;
using NTS.Witness.Constants;

namespace NTS.Witness.Components.Layout;

public partial class MainLayout
{
    bool _drawerOpen = false;
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
    public bool IsCurrentPageEmergencyContacts => Navigator.CurrentEndpoint.Contains(Endpoints.EMERGENCY_CONTACTS);

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
