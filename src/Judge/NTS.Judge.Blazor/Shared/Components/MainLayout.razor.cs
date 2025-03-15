using MudBlazor;
using Not.Blazor.Components;
using NTS.Judge.Blazor.Setup.Settings;

namespace NTS.Judge.Blazor.Shared.Components;

public partial class MainLayout
{
    MudThemeProvider _themeProvider = default!;
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

    async void ToggleLayoutVisibility()
    {
        _hideLayout = !_hideLayout;
        await InvokeAsync(StateHasChanged);
    }
}
