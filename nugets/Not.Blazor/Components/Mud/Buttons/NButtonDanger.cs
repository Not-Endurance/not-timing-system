using MudBlazor;

namespace Not.Blazor.Components;

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
