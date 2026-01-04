using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Ports;
using Not.Safe;

namespace Not.Blazor.CRUD.Forms.Components;

public partial class FormUpdateDialog<T, TForm>
    where T : new()
    where TForm : NForm<T>
{
    NDynamic<T, TForm>? _dynamicForm;

    [Inject]
    IUpdateBehind<T> Behind { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public T Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }

    async Task Update()
    {
        await SafeHelper.Run(Submit, InjectValidation);
    }

    async Task Submit()
    {
        await Behind.Update(Model);
        var dialogResult = DialogResult.Ok(true);
        Dialog.Close(dialogResult);
    }

    async Task InjectValidation(ValidationException validation)
    {
        await _dynamicForm!.Instance.AddValidationError(validation.Property, validation.Message);
    }
}
