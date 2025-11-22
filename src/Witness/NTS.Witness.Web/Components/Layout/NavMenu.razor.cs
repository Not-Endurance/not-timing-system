using Microsoft.AspNetCore.Components;

namespace NTS.Witness.Web.Components.Layout;

public partial class NavMenu
{
    [Parameter]
    public Action AfterNavigationAction { get; set; } = default!;
}
