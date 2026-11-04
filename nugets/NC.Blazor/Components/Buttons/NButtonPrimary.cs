// ReSharper disable CheckNamespace

using MudBlazor;
using NC.Blazor.Components.Buttons.Abstractions;

namespace NC.Blazor.Components.Buttons;

public class NButtonPrimary : NButtonBase
{
    public NButtonPrimary()
    {
        Color = Color.Primary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
