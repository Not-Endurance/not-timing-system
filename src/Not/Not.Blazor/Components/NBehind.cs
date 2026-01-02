using Not.Blazor.Ports;
using Not.Safe;

namespace Not.Blazor.Components;

public class NBehind : ComponentBase
{
    [Parameter]
    public string? Style { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public bool IsInitialized { get; private set; } = true;

    protected async Task Observe(INObservable observable)
    {
        await Observe(observable, []);
    }

    protected async Task Observe(INObservable observable, params IEnumerable<object> arguments)
    {
        IsInitialized = false;
        await Render();
        observable.Subscribe(OnEmit);
        await observable.Initialize(arguments);
        IsInitialized = true;
        await Render();
    }

    protected async Task Render()
    {
        OnBeforeRender();
        await InvokeAsync(StateHasChanged);
    }

    protected virtual void OnBeforeRender() { }

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

    async Task OnEmit()
    {
        await Render();
    }
}
