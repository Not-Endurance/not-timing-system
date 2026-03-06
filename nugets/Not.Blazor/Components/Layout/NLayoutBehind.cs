using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Layout;

public class NLayoutBehind : LayoutComponentBase
{
    protected bool DrawerOpen { get; set; } = true;
    protected bool HideLayout { get; private set; }
    protected NTheme Theme { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Watermark { get; set; } = default!;

    [Parameter]
    public RenderFragment? Content { get; set; }

    [Parameter]
    public RenderFragment? Drawer { get; set; }

    [Parameter]
    public string? MenuTitle { get; set; }

    protected override void OnInitialized()
    {
        PrintableComponent.OnToggle(ToggleLayoutVisibility);
    }

    protected void ToggleDrawer()
    {
        DrawerOpen = !DrawerOpen;
    }

    protected async Task ToggleLayoutVisibility() // TODO: fix all async voids possible
    {
        HideLayout = !HideLayout;
        await InvokeAsync(StateHasChanged);
    }
}
