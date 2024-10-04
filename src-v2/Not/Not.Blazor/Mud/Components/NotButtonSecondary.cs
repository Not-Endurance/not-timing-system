using MudBlazor;
using Not.Blazor.Mud.Components.Base;

namespace Not.Blazor.Mud.Components;

public class NotButtonSecondary : NotButtonBase
{
    public NotButtonSecondary()
    {
        Color = Color.Secondary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
