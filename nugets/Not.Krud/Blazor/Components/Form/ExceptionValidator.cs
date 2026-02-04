using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Not.Exceptions;
using Not.Notify;

namespace Not.Krud.Blazor.Components.Form;

public class ExceptionValidator : ComponentBase
{
    ValidationMessageStore? _messages;

    [CascadingParameter]
    EditContext EditContext { get; set; } = default!;

    [Inject]
    IServiceProvider ServiceProvider { get; set; } = default!;

    protected override void OnInitialized()
    {
        _messages = new(EditContext);
        EditContext.EnableDataAnnotationsValidation(ServiceProvider);
    }

    internal void Reset()
    {
        GuardHelper.ThrowIfDefault(_messages);
        _messages.Clear();
        EditContext.NotifyValidationStateChanged();
    }

    internal void ShowErrors(IEnumerable<ValidationException> validations)
    {
        GuardHelper.ThrowIfDefault(_messages);
        Reset();
        foreach (var exception in validations)
        {
            if (exception.Property == null)
            {
                NotifyHelper.Warn(exception.Message);
            }
            else
            {
                _messages.Add(EditContext.Field(exception.Property), exception.Message);
            }
        }
        EditContext.NotifyValidationStateChanged();
    }
}
