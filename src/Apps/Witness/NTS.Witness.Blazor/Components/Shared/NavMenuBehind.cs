using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor.Components.Shared;

public class NavMenuBehind : NComponent
{
    [Parameter, EditorRequired]
    public Action OnNavigation { get; set; } = default!;
}
