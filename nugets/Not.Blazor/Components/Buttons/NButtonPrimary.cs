// ReSharper disable CheckNamespace

using MudBlazor;
using Not.Blazor.Components.Buttons.Abstractions;

namespace Not.Blazor.Components.Buttons;

public class NButtonPrimary : NButtonBase
{
    public NButtonPrimary()
    {
        Color = Color.Primary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
