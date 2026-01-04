using Not.Blazor.Components;

namespace NTS.Witness.Blazor.Components.Shared;

public class NavMenuBehind : NBehind
{
    [Parameter, EditorRequired]
    public Action OnNavigation { get; set; } = default!;
}
