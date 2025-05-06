using Not.Blazor.Components;
using Not.Blazor.Components.Mud;
using Not.Startup;

namespace NTS.Judge.Blazor.Shared.Components;

public partial class MainLayout
{
    NTheme _mudTheme = default!;
    bool _hideLayout;
    bool _drawerOpen = true;

    [Inject]
    IEnumerable<IStartupInitializerAsync> AsyncInitializers { get; set; } = default!;

    protected override void OnInitialized()
    {
        PrintableComponent.OnToggle(ToggleLayoutVisibility);
    }

    protected override async Task OnInitializedAsync()
    {
        foreach (var initializer in AsyncInitializers)
        {
            await initializer.RunAtStartupAsync();
        }
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
