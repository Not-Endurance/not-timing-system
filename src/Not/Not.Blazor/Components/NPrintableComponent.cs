using Not.Blazor.Ports;
using Not.Events;

namespace Not.Blazor.Components;

public abstract class PrintableComponent : NComponent, IDisposable
{
    public delegate void ToggleVisibility();

    public static void OnToggle(Action handler)
    {
        _toggleEvent.Subscribe(handler);
    }

    static Event _toggleEvent = new();

    [Inject]
    IPrintInterop PrintInterop { get; set; } = default!;

    protected bool IsButtonVisible { get; private set; }

    protected override void OnInitialized()
    {
        _toggleEvent.Subscribe(VisibilityToggleHook);
    }

    protected async Task OpenPrintDialog()
    {
        InvokeToggle();
        await PrintInterop.OpenPrintDialog();
        InvokeToggle();
    }

    /// <summary>
    /// Make sure to Rerender when overriding this method otherwise changes might not be reflected
    /// </summary>
    protected virtual void VisibilityToggleHook() { }

    public void Dispose()
    {
        // Figure out how to unsubscribe at instance level. Consider ditching static events?
        //_toggleEvent.UnsubscribeAll();
    }

    void InvokeToggle()
    {
        _toggleEvent.Emit();
    }
}
