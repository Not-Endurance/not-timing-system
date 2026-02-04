using Microsoft.AspNetCore.Components;
using Not.Blazor.Components;
using Not.Blazor.Navigation;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Form;

namespace Not.Krud.Blazor.Components;

public abstract class KrudFormBehind<TModel> : NComponent
    where TModel : IKrudFormModel
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected ExceptionValidator ValidatorRef { get; set; } = default!;

    protected bool IsCreateForm => Model.Id == null;
    
    [Parameter, EditorRequired]
    public Func<Task> Create { get; set; } = default!;

    [Parameter, EditorRequired]
    public Func<Task> Update { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter, EditorRequired]
    public TModel Model { get; set; } = default!;

    [Parameter]
    public bool DisableBack { get; set; }

    public async Task OnSubmit()
    {
        try
        {
            ValidatorRef.Reset();

            if (IsCreateForm)
            {
                await Create();
            }
            else
            {
                await Update();
            }
        }
        catch (ValidationException ex)
        {
            ValidatorRef.ShowErrors([ex]);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    public bool CanNavigateBack()
    {
        try
        {
            return !DisableBack && Navigator.CanNavigateBack();
        }
        catch (Exception ex)
        {
            Handle(ex);
            return false;
        }
    }

    public void NavigateBack()
    {
        try
        {
            if (CanNavigateBack())
            {
                Navigator.NavigateBack();
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
