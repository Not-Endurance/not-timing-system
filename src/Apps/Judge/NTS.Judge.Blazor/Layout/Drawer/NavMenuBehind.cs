using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation;
using NTS.Judge.Features.Core.State;
using NTS.Judge.Features.Settings;
using static NTS.Judge.Blazor.Routes;

namespace NTS.Judge.Blazor.Layout.Drawer;

public class NavMenuBehind : NStatefulComponent
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [Inject]
    protected ISettingBehind SettingBehind { get; set; } = default!;

    [Inject]
    protected ITimingStateService TimingStateService { get; set; } = default!;

    protected override void OnInitialized()
    {
        LandNavigator.Initialize(HOME);
    }

    protected override async Task OnInitializedAsync()
    {
        // Necessary in order to initialize the settings on startup
        // TODO: what do if no internet?
        await Observe(SettingBehind);
        await Observe(TimingStateService);
    }
}
