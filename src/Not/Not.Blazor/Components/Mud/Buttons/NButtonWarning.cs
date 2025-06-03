// ReSharper disable CheckNamespace

using MudBlazor;

namespace Not.Blazor.Components;

public class NButtonWarning : NButtonBase
{
    public NButtonWarning()
    {
        Size = Size.Medium;
        Color = Color.Warning;
        Variant = Variant.Filled;
        StartIcon = Icons.Material.Rounded.DeleteForever;
    }
}
