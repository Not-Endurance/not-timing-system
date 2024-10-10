using MudBlazor;
using Not.Blazor.Mud.Components.Base;

namespace Not.Blazor.Mud.Components;

public class NotButtonDanger : NotButtonBase
{
    public NotButtonDanger()
    {
        Size = Size.Medium;
        Color = Color.Error;
        Variant = Variant.Filled;
        StartIcon = Icons.Material.Rounded.DeleteForever;
    }
}
