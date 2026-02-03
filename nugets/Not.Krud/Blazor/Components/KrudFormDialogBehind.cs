using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components;
using Not.Exceptions;
using Not.Krud.Blazor.Components.Abstractions;

namespace Not.Krud.Blazor.Components;

public class KrudFormDialogBehind<TModel, TForm> : NComponent
    where TModel : new()
    where TForm : KrudFormContainer<TModel>
{
    protected KrudDynamic<TModel, TForm>? DynamicForm { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public Func<TModel, Task> Submit { get; set; } = default!;

    [Parameter]
    public TModel Model { get; set; } = new();

    protected async Task OnSubmit()
    {
        try
        {
            await Submit(Model);
            var dialogResult = DialogResult.Ok(Model);
            Dialog.Close(dialogResult);
        }
        catch (ValidationException validation)
        {
            await DynamicForm!.Instance.AddValidationError(validation.Property, validation.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
