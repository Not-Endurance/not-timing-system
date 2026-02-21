using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using Not.Krud.Abstractions;

namespace Not.Krud.Blazor.Components.Abstractions;

public abstract class KrudShell<TModel> : NComponent
    where TModel : IKrudFormModel, new()
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    IKrudFormService<TModel> Service { get; set; } = default!;

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
