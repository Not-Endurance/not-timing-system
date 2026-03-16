using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using Not.Krud.Abstractions;
using Not.Notify;

namespace Not.Krud.Blazor.Components.Abstractions;

public abstract class KrudShell<TModel> : NComponent
    where TModel : IKrudFormModel, new()
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    IKrudFormService<TModel> Service { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Parameter]
    public TModel Model { get; set; } = new();

    [Parameter]
    public RenderFragment? AdditionalContent { get; set; }

    [Parameter]
    public RenderFragment? FormButtons { get; set; }

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
        Notifier.Success(Updated_string);
    }
}
