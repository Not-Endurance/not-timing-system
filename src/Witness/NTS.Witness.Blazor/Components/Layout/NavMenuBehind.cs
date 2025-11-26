using Not.Blazor.Components;

namespace NTS.Witness.Blazor.Components.Layout;

public class NavMenuBehind : NComponent
{
    [Parameter, EditorRequired]
    public Action AfterNavigationAction { get; set; } = default!;
}
