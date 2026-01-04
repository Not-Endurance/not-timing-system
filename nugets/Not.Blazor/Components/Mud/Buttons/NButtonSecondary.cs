using MudBlazor;

namespace Not.Blazor.Components;

public class NButtonSecondary : NButtonBase
{
    public NButtonSecondary()
    {
        Color = Color.Secondary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
