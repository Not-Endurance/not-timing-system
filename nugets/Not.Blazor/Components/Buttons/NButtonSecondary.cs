using MudBlazor;
using Not.Blazor.Components.Buttons.Abstractions;

namespace Not.Blazor.Components.Buttons;

public class NButtonSecondary : NButtonBase
{
    public NButtonSecondary()
    {
        Color = Color.Secondary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
