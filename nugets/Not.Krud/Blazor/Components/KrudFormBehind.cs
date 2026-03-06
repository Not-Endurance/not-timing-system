using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Krud.Blazor.Components.Form;

namespace Not.Krud.Blazor.Components;

public abstract class KrudFormBehind<TModel> : NComponent
    where TModel : IKrudFormModel, new()
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected ExceptionFormValidator ValidatorRef { get; set; } = default!;

    protected bool IsCreateForm => Shell.Model.Id == null;

    /// <summary>
    /// The calling components should provide a reference to a Shell instance.
    /// The intended usage is for the calling component to inherit from <see cref="KrudShell{TModel}"/>
    /// but you can also create a separate shell component and pass a reference here.
    /// </summary>
    [Parameter, EditorRequired]
    public KrudShell<TModel> Shell { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool DisableBack { get; set; }

    public async Task OnSubmit()
    {
        try
        {
            ValidatorRef.Reset();

            if (IsCreateForm)
            {
                await Shell.Create();
            }
            else
            {
                await Shell.Update();
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
