using System.Linq.Expressions;
using MudBlazor;
using Not.Blazor.Components.Input.Internal;

namespace Not.Blazor.Components.Input;

public class NTextField<T> : MudTextField<T>
{
    public NTextField()
    {
        Margin = Margin.Dense;
    }

    protected override void OnParametersSet()
    {
        ForRequiredValidator.ValidateFor(this);
        base.OnParametersSet();
    }
}
