using Microsoft.AspNetCore.Components.Routing;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;

namespace Not.Blazor.Components.Layout;

public class NNavLinkBehind : NComponent
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Endpoint { get; set; } = default!;

    [Parameter]
    public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;

    [Parameter]
    public Action? AfterNavigation { get; set; }

    [Parameter]
    public string Icon { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected void Land()
    {
        try
        {
            LandNavigator.LandTo(Endpoint);
            AfterNavigation?.Invoke();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
