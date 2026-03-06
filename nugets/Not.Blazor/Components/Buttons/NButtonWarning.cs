// ReSharper disable CheckNamespace

using MudBlazor;
using Not.Blazor.Components.Buttons.Abstractions;

namespace Not.Blazor.Components.Buttons;

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
