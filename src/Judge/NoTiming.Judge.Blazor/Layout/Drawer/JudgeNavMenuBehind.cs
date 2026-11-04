using NC.Blazor.Components.Abstractions;
using NC.Blazor.Navigation.Abstractions;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Judge.Contracts.Features.Settings;
using static NoTiming.Judge.Blazor.JudgeRoutes;

namespace NoTiming.Judge.Blazor.Layout.Drawer;

public class JudgeNavMenuBehind : NStatefulComponent
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [Inject]
    protected ISettingService SettingService { get; set; } = default!;

    [Inject]
    protected INoTimingSocketService SocketService { get; set; } = default!;

    protected bool HasActiveEvent => SocketService.Event != null;

    protected override void OnInitialized()
    {
        LandNavigator.Initialize(HOME);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SettingService);
        await Observe(SocketService);
    }
}
