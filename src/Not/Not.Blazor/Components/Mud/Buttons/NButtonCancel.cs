using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Not.Blazor.Components;

public class NButtonCancel : NButtonSecondary
{
    public NButtonCancel()
    {
        Variant = Variant.Outlined;
        Text = Cancel_string;
    }
}
