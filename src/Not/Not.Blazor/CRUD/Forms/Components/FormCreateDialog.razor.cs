using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Ports;
using Not.Safe;

namespace Not.Blazor.CRUD.Forms.Components;

public partial class FormCreateDialog<T, TForm>
    where T : new()
    where TForm : NForm<T>
{
    T _model = new();
    NDynamic<T, TForm>? _dynamicForm;

    [Inject]
    ICreateBehind<T> Behind { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance Dialog { get; set; } = default!;

    async Task Create()
    {
        await SafeHelper.Run(Submit, InjectValidation);
    }

    async Task Submit()
    {
        await Behind.Create(_model);
        var dialogResult = DialogResult.Ok(true);
        Dialog.Close(dialogResult);
    }

    async Task InjectValidation(ValidationException validation)
    {
        await _dynamicForm!.Instance.AddValidationError(validation.Property, validation.Message);
    }
}
