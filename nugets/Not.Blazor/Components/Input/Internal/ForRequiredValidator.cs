using System.Linq.Expressions;
using MudBlazor;
using Not.Notify;

namespace Not.Blazor.Components.Input.Internal;

internal class ForRequiredValidator
{
    public static void ValidateFor<T>(MudBaseInput<T> mudBaseInput)
    {
        if (mudBaseInput.For == null)
        {
            Validate(mudBaseInput.Label, mudBaseInput.GetType(), typeof(T));
        }
    }

    public static void ValidateFor<T>(MudSwitch<T> switchInput)
    {
        if (switchInput.For == null)
        {
            Validate(switchInput.Label, switchInput.GetType(), typeof(T));
        }
    }

    public static void ValidateFor<T>(MudPicker<T> mudDatePicker)
    {
        if (mudDatePicker.For == null)
        {
            Validate(mudDatePicker.Label, mudDatePicker.GetType(), typeof(T));
        }
    }

    static void Validate(string? label, Type inputType, Type genericType)
    {
        var identifier = label ?? $"{inputType.Name}({genericType.Name})";
        NotifyHelper.Error($"Parameter 'For' is not provided on '{identifier}'. Form validation will not work for that field");
    }
}
