using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Exceptions;
using Not.Safe;

namespace Not.Krud.Blazor.Components;

public class KrudUpdateFormDialogBehind<TModel, TForm> : NComponent
    where TModel : new()
    where TForm : KrudFormBehindNotSure<TModel>
{
    protected KrudDynamic<TModel, TForm>? _dynamicForm;

    [Inject]
    IUpdateBehind<TModel> Behind { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public TModel Model { get; set; } = default!;

    protected async Task Update()
    {
        await SafeHelper.Run(Submit, InjectValidation);
    }

    protected async Task Submit()
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
