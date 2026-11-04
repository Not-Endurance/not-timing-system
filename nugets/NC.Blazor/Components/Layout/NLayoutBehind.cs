using Microsoft.Extensions.Localization;
using Not.Blazor.Components.Abstractions;
using Not.Localization;
using Not.Notify;
using Not.Safe;
using Not.Startup;

namespace Not.Blazor.Components.Layout;

public class NLayoutBehind : LayoutComponentBase, IDisposable
{
    bool _hasStarted;

    [Inject]
    IEnumerable<IStartupInitializer> Initializers { get; set; } = default!;

    [Inject]
    IEnumerable<IStartupInitializerAsync> AsyncInitializers { get; set; } = default!;

    [Inject]
    IStringLocalizer? StringLocalizer { get; set; }

    [Inject]
    INotifier Notifier { get; set; } = default!;

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
        LocalizationHelper.Configure(StringLocalizer);
        NotificationHelper.Configure(Notifier);
        PrintableComponent.OnToggle(ToggleLayoutVisibility);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasStarted)
        {
            return;
        }

        _hasStarted = true;

        try
        {
            foreach (var initializer in Initializers)
            {
                initializer.RunAtStartup();
            }
            foreach (var initializer in AsyncInitializers)
            {
                await initializer.RunAtStartupAsync();
            }
        }
        catch (Exception ex)
        {
            SafeHelper.HandleException(ex);
        }
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

    public void Dispose()
    {
        LocalizationHelper.Clear(StringLocalizer);
        NotificationHelper.Clear(Notifier);
    }
}
