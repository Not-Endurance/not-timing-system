using MudBlazor;
using Not.Blazor.Components.Input.Internal;

namespace Not.Blazor.Components.Input;

public class NDatePicker : MudDatePicker
{
    protected override void OnParametersSet()
    {
        ForRequiredValidator.ValidateFor(this);
        base.OnParametersSet();
    }
}
