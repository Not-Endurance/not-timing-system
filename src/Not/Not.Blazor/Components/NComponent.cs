using Not.Blazor.Ports;
using Not.Localization;

namespace Not.Blazor.Components;

public class NComponent : ComponentBase
{
    [Inject]
    protected ILocalizer Localizer { get; set; } = default!;

    [Parameter]
    public string? Style { get; set; }

    [Parameter]
    public string? Class { get; set; }

    public bool IsInitialized { get; private set; }

    protected async Task Observe(IObservableBehind observable, params IEnumerable<object> arguments)
    {
        observable.Subscribe(OnEmit);
        await observable.Initialize(arguments);
        IsInitialized = true;
        await Render();
    }

    protected async Task Render()
    {
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

    string CombineWithSpace(params string?[] values)
    {
        var filtered = values.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" ", filtered);
    }

    async Task OnEmit()
    {
        OnBeforeRender();
        await Render();
    }
}
