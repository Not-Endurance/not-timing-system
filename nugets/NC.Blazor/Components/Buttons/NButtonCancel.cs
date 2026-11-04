using MudBlazor;
using NC.Blazor.Components.Buttons.Abstractions;

// ReSharper disable once CheckNamespace
namespace NC.Blazor.Components.Buttons;

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
