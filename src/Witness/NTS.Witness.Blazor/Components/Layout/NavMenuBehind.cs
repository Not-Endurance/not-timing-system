using Not.Blazor.Components;

namespace NTS.Witness.Blazor.Components.Layout;

public class NavMenuBehind : NComponent
{
    [Parameter]
    public Action AfterNavigationAction { get; set; } = default!;
}
