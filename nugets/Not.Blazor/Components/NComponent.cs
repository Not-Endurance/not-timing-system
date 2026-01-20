using Not.Application.Behinds.Adapters;
using Not.Notify;
using Not.Observables;
using Not.Safe;

namespace Not.Blazor.Components;

public class NComponent : ComponentBase
{
    [Parameter]
    public string? Style { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public bool IsInitialized { get; private set; } = true;

    protected async Task Observe(IStatefulService statefulService)
    {
        await Observe(statefulService, []);
    }

    protected void Observe(IObservable observable)
    {
        observable.Event.Subscribe(Render); // TODO: Coalesce renders
    }

    protected async Task Observe(IStatefulService statefulService, params IEnumerable<object> arguments)
    {
        IsInitialized = false;
        Observe((IObservable)statefulService);
        await statefulService.Initialize(arguments);
        IsInitialized = true;
        await Render();
    }

    protected async Task Render()
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
