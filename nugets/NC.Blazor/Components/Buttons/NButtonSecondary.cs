using MudBlazor;
using NC.Blazor.Components.Buttons.Abstractions;

namespace NC.Blazor.Components.Buttons;

public class NButtonSecondary : NButtonBase
{
    public NButtonSecondary()
    {
        Color = Color.Secondary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
