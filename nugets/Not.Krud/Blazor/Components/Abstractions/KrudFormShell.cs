using Microsoft.AspNetCore.Components;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Blazor.Navigation;

namespace Not.Krud.Blazor.Components.Abstractions;

public abstract class KrudFormShell<TModel> : NComponent
    where TModel : new()
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    IFormBehind<TModel> Service { get; set; } = default!;

    /// <summary>
    /// Define the action called on Form submission. Make sure not to swallow <seealso cref="Not.Exceptions.ValidationException"/>
    /// because Krud validation relies internally on them to render Form validation messages
    /// </summary>
    /// <returns></returns>
    //protected abstract Task SubmitActionSafe();

    [Parameter]
    public TModel Model { get; set; } = new();

    /// <summary>
    /// A hook for Form callers to attach Submit handlers such as <seealso cref="Form.KrudFormDialog{TModel, TForm}" closing the dialog />
    /// </summary>
    [Parameter]
    public Func<TModel, Task>? OnSubmit { get; set; } = default!;

    protected void NavigateBack()
    {
        if (Navigator.CanNavigateBack())
        {
            Navigator.NavigateBack();
        }
    }

    internal async Task Create()
    {
        await Service.Create(Model);
        if (OnSubmit != null)
        {
            await OnSubmit(Model);
        }
    }

    internal async Task Update()
    {
        await Service.Update(Model);
        if (OnSubmit != null)
        {
            await OnSubmit(Model);
        }
        NavigateBack();
    }
}
