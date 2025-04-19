using Not.Blazor.Components;
using Not.Blazor.Components.Mud;

namespace NTS.Judge.Blazor.Shared.Components;

public partial class MainLayout
{
    NMudProviders _mudProviders = default!;
    bool _hideLayout;
    bool _drawerOpen = true;

    protected override void OnInitialized()
    {
        PrintableComponent.OnToggle(ToggleLayoutVisibility);
    }

    void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    async void ToggleLayoutVisibility() // TODO: fix all async voids possible
    {
        _hideLayout = !_hideLayout;
        await InvokeAsync(StateHasChanged);
    }
}
