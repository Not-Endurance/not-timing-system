using Microsoft.AspNetCore.Components.Routing;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;

namespace Not.Blazor.Components.Layout;

public class NNavLinkBehind : NComponent
{
    [Inject]
    ILandNavigator LandNavigator { get; set; } = default!;

    [CascadingParameter(Name = "CloseResponsiveDrawer")]
    Func<Task>? CloseResponsiveDrawer { get; set; }

    [Parameter, EditorRequired]
    public string Endpoint { get; set; } = default!;

    [Parameter]
    public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;

    [Parameter]
    public bool MenuItem { get; set; }

    [Parameter]
    public Action? AfterNavigation { get; set; }

    [Parameter]
    public string Icon { get; set; } = default!;

    protected async Task Land()
    {
        try
        {
            LandNavigator.LandTo(Endpoint);
            AfterNavigation?.Invoke();
            if (CloseResponsiveDrawer != null)
            {
                await CloseResponsiveDrawer();
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
