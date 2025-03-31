using Microsoft.AspNetCore.Components.Routing;
using Not.Blazor.Navigation;

namespace Not.Blazor.Components;

public partial class NNavLink : NComponent
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [Inject]
    NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Endpoint { get; set; } = default!;

    [Parameter]
    public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;

    void Land()
    {
        LandNavigator.LandTo(Endpoint);
    }
}
