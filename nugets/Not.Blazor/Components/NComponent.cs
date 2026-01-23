using Not.Application.Behinds.Adapters;
using Not.Concurrency;
using Not.Notify;
using Not.Observables;
using Not.Safe;

namespace Not.Blazor.Components;

public class NComponent : ComponentBase
{
    CoalesceInvoker _coalescedRender;

    public NComponent()
    {
        _coalescedRender = new(async () =>
        {
            try
            {
                OnBeforeRender();
                await OnBeforeRenderAsync();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotifyHelper.Error(ex);
            }
        });
    }

    [Parameter]
    public string? Style { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected Task InvokeRender()
    {
        return _coalescedRender.Invoke();
    }

    protected virtual void OnBeforeRender() { }

    protected virtual Task OnBeforeRenderAsync() 
    {
        return Task.CompletedTask;
    }

    protected string CombineClass(string customClass)
    {
        return CombineWithSpace(Class, customClass);
    }

    protected string CombineStyle(string customStyle)
    {
        return CombineWithSpace(Style, customStyle);
    }

    protected void Handle(Exception ex)
    {
        SafeHelper.HandleException(ex);
    }

    string CombineWithSpace(params string?[] values)
    {
        var filtered = values.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" ", filtered);
    }
}
