using MudBlazor;
using Not.Blazor.Mud.Components.Base;

namespace Not.Blazor.Mud.Components;

public class NotButtonSecondary : NotButtonBase
{
    public NotButtonSecondary()
    {
        Color = Color.Primary;
        Variant = Variant.Outlined;
        IconSize = Size.Medium;
    }
}
