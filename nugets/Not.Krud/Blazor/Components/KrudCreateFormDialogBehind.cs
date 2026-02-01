using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Exceptions;
using Not.Safe;

namespace Not.Krud.Blazor.Components;

public class KrudCreateFormDialogBehind<TModel, TForm> : NComponent
    where TModel : new()
    where TForm : KrudFormBehindNotSure<TModel>
{
    protected TModel _model = new();
    protected KrudDynamic<TModel, TForm>? _dynamicForm;

    [Inject]
    ICreateBehind<TModel> Behind { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance Dialog { get; set; } = default!;

    protected async Task Create()
    {
        await SafeHelper.Run(Submit, InjectValidation);
    }

    async Task Submit()
    {
        await Behind.Create(_model);
        var dialogResult = DialogResult.Ok(_model);
        Dialog.Close(dialogResult);
    }

    async Task InjectValidation(ValidationException validation)
    {
        await _dynamicForm!.Instance.AddValidationError(validation.Property, validation.Message);
    }
}
