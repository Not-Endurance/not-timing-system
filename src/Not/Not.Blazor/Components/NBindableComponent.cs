using System.ComponentModel.DataAnnotations;

namespace Not.Blazor.Components;

public class NBindableComponent<T> : NComponent
{
    T? _value;

    [Parameter, Required]
#pragma warning disable BL0007 // Component parameters should be auto properties
    public T? Value
#pragma warning restore BL0007 // Component parameters should be auto properties
    {
        get => _value;
        set
        {
            if (_value?.GetHashCode() == value?.GetHashCode())
            {
                return;
            }
            _value = value;
            ValueChanged.InvokeAsync(value);
        }
    }

    [Parameter]
    public EventCallback<T> ValueChanged { get; set; }
}
