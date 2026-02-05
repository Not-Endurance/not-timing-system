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
            var fieldIdentifier = mudBaseInput.Label ?? $"{mudBaseInput.GetType().Name}({typeof(T).Name})";
            NotifyHelper.Error(
                $"Parameter '{nameof(mudBaseInput.For)}' is not provided on '{fieldIdentifier}'. " +
                $"Form validation will not work for that field");
        }
    }
}
