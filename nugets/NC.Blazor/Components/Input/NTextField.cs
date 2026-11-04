using MudBlazor;

namespace NC.Blazor.Components.Input;

public class NTextField<T> : MudTextField<T>
{
    public NTextField()
    {
        Margin = Margin.Dense;
    }
}
