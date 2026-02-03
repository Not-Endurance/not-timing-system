using Microsoft.AspNetCore.Components;
using Not.Blazor.Navigation;
using Not.Exceptions;
using Not.Krud.Blazor.Components.Abstractions;

namespace Not.Krud.Blazor.Components;

public abstract class KrudFormBehind<TModel> : KrudFormContainer<TModel>
{
    //public NDynamic<TModel, TForm> Form = default!;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
    
    [Parameter, EditorRequired]
    public Func<TModel, Task> FormAction { get; set; } = default!;

    [Parameter, EditorRequired]
    public string ButtonText { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter, EditorRequired]
    public TModel Model { get; set; } = default!;

    [Parameter]
    public bool DisableBack { get; set; }

    protected async Task InjectValidation(ValidationException _)
    {
        await Task.CompletedTask;
        //await Form!.Instance.AddValidationError(validation.Property, validation.Message);
    }

    public async Task Submit()
    {
        await FormAction(Model);
    }

    public bool CanNavigateBack()
    {
        return !DisableBack && Navigator.CanNavigateBack();
    }

    public void NavigateBack()
    {
        if (CanNavigateBack())
        {
            Navigator.NavigateBack();
        }
    }
}
