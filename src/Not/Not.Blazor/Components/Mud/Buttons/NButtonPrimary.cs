// ReSharper disable CheckNamespace

using MudBlazor;

namespace Not.Blazor.Components;

public class NButtonPrimary : NButtonBase
{
    public NButtonPrimary()
    {
        Color = Color.Primary;
        Variant = Variant.Filled;
        IconSize = Size.Medium;
    }
}
