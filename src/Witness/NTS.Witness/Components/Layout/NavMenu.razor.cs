using Microsoft.AspNetCore.Components;

namespace NTS.Witness.Components.Layout;

public partial class NavMenu
{
    [Parameter]
    public Action AfterNavigationAction { get; set; } = default!;
}
