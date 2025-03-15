using Not.Blazor.Navigation;
using NTS.Judge.Blazor.Setup.Settings;
using static NTS.Judge.Blazor.Shared.Constants.Endpoints;

namespace NTS.Judge.Blazor.Shared.Components.Nav;

public partial class NavMenu
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [Inject]
    protected ISettingBehind SettingBehind { get; set; } = default!;

    protected override void OnInitialized()
    {
        LandNavigator.Initialize(ENDURANCE_EVENT_PAGE);
    }

    protected override async Task OnInitializedAsync()
    {
        // Necessary in order to initialize the settings on startup
        // TODO: what do if no internet?
        await Observe(SettingBehind);
    }
}
