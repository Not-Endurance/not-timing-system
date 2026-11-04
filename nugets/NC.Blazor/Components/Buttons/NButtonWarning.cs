// ReSharper disable CheckNamespace

using MudBlazor;
using NC.Blazor.Components.Buttons.Abstractions;

namespace NC.Blazor.Components.Buttons;

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
