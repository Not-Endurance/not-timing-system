using Microsoft.AspNetCore.Components;
using Not.Blazor.Navigation;
using NTS.Witness.Constants;

namespace NTS.Witness.Components.Layout;

public partial class MainLayout
{
    bool _drawerOpen = false;

    void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    void CloseDrawer()
    {
        _drawerOpen = false;
    }
}
