using MudBlazor;
using Not.Blazor.Components.Input.Internal;

namespace Not.Blazor.Components;

public class NSwitch<T> : MudSwitch<T>
{
    public NSwitch()
    {
        Color = Color.Primary;
    }

    protected override void OnParametersSet()
    {
        ForRequiredValidator.ValidateFor(this);
        base.OnParametersSet();
    }
}
