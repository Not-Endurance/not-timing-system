using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using NTS.Application.Socket;
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
    protected INtsSocketService SocketService { get; set; } = default!;

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
