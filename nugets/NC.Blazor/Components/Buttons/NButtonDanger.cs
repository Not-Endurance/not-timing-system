using MudBlazor;
using NC.Blazor.Components.Buttons.Abstractions;

namespace NC.Blazor.Components.Buttons;

public class NButtonDanger : NButtonBase
{
    public NButtonDanger()
    {
        Size = Size.Medium;
        Color = Color.Error;
        Variant = Variant.Filled;
        StartIcon = Icons.Material.Rounded.DeleteForever;
    }
}
