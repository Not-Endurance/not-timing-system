using Not.Blazor.Components;

namespace Not.Blazor.Print;

public abstract class PrintableComponent : NotComponent, IDisposable
{
    public delegate void ToggleVisibility();
    public static ToggleVisibility? ToggleVisibilityEvent;

    [Inject]
    IPrintInterop _printInterop { get; set; } = default!;

    protected bool IsButtonVisible { get; private set; }

    protected override void OnInitialized()
    {
        ToggleVisibilityEvent += VisibilityToggleHook;
    }

    protected async Task OpenPrintDialog()
    {
        InvokeToggle();
        await _printInterop.OpenPrintDialog();
        InvokeToggle();
    }

    /// <summary>
    /// Make sure to Rerender when overriding this method otherwise changes might not be reflected
    /// </summary>
    protected virtual void VisibilityToggleHook()
    {
    }

    public void Dispose()
    {
        ToggleVisibilityEvent -= VisibilityToggleHook;
    }

    void InvokeToggle()
    {
        ToggleVisibilityEvent?.Invoke();
    }
}
