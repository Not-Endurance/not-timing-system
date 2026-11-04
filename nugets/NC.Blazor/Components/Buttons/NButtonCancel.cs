using MudBlazor;
using Not.Blazor.Components.Buttons.Abstractions;

// ReSharper disable once CheckNamespace
namespace Not.Blazor.Components.Buttons;

public class NButtonCancel : NButtonBase
{
    public NButtonCancel()
    {
        Variant = Variant.Outlined;
        Color = Color.Tertiary;
        Size = Size.Medium;
        Text = Cancel_string;
    }
}
