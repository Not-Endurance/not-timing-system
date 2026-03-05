using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using NTS.Judge.Features.Core.State;
using NTS.Judge.Features.Settings;
using static NTS.Judge.Blazor.Routes;

namespace NTS.Judge.Blazor.Layout.Drawer;

public class NavMenuBehind : NStatefulComponent
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [Inject]
    protected ISettingService SettingService { get; set; } = default!;

    [Inject]
    protected ITimingStateService TimingStateService { get; set; } = default!;

    protected override void OnInitialized()
    {
        LandNavigator.Initialize(HOME);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SettingService);
        await Observe(TimingStateService);
    }
}
