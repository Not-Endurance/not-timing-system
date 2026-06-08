using MudBlazor;
using MudBlazor.Services;
using Not.Async;
using Not.Notify;
using Not.Safe;

namespace Not.Blazor.Components.Abstractions;

public class NComponent : NComponentBase, IDisposable
{
    readonly Guid _browserViewportObserverId = Guid.NewGuid();
    CoalesceInvoker _coalescedRender;
    bool _isObservingBreakpointChanges;

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
                Notifier.Error(ex);
            }
        });
    }

    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Inject]
    protected IBrowserViewportService ViewportService { get; set; } = default!;

    protected virtual bool ObserveBreakpointChanges => false;
    protected Breakpoint CurrentBreakpoint { get; private set; } = Breakpoint.Always;
    protected bool IsXs => CurrentBreakpoint == Breakpoint.Xs;
    protected bool IsSmAndDown => IsBreakpointAtOrBelow(Breakpoint.Sm);
    protected bool IsMdAndDown => IsBreakpointAtOrBelow(Breakpoint.Md);
    protected bool IsLgAndDown => IsBreakpointAtOrBelow(Breakpoint.Lg);
    protected bool IsSmAndUp => IsBreakpointAtOrAbove(Breakpoint.Sm);
    protected bool IsMdAndUp => IsBreakpointAtOrAbove(Breakpoint.Md);
    protected bool IsLgAndUp => IsBreakpointAtOrAbove(Breakpoint.Lg);

    protected Task InvokeRender()
    {
        return _coalescedRender.Invoke();
    }

    protected virtual void OnBeforeRender() { }

    protected virtual Task OnBeforeRenderAsync()
    {
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || !ObserveBreakpointChanges || _isObservingBreakpointChanges)
        {
            return;
        }

        try
        {
            await ViewportService.SubscribeAsync(
                _browserViewportObserverId,
                OnBrowserViewportChanged,
                new ResizeOptions { NotifyOnBreakpointOnly = true },
                fireImmediately: true
            );
            _isObservingBreakpointChanges = true;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void Handle(Exception ex)
    {
        SafeHelper.HandleException(ex);
    }

    protected bool IsBreakpointAtOrBelow(Breakpoint breakpoint)
    {
        return GetBreakpointOrder(CurrentBreakpoint) <= GetBreakpointOrder(breakpoint);
    }

    protected bool IsBreakpointAtOrAbove(Breakpoint breakpoint)
    {
        return GetBreakpointOrder(CurrentBreakpoint) >= GetBreakpointOrder(breakpoint);
    }

    public virtual void Dispose()
    {
        if (_isObservingBreakpointChanges)
        {
            _ = ViewportService.UnsubscribeAsync(_browserViewportObserverId);
        }

        GC.SuppressFinalize(this);
    }

    async Task OnBrowserViewportChanged(BrowserViewportEventArgs args)
    {
        if (CurrentBreakpoint == args.Breakpoint)
        {
            return;
        }

        CurrentBreakpoint = args.Breakpoint;
        await InvokeRender();
    }

    static int GetBreakpointOrder(Breakpoint breakpoint)
    {
        return breakpoint switch
        {
            Breakpoint.Xs => 0,
            Breakpoint.Sm => 1,
            Breakpoint.Md => 2,
            Breakpoint.Lg => 3,
            Breakpoint.Xl => 4,
            Breakpoint.Xxl => 5,
            Breakpoint.SmAndDown => 1,
            Breakpoint.MdAndDown => 2,
            Breakpoint.LgAndDown => 3,
            Breakpoint.XlAndDown => 4,
            Breakpoint.SmAndUp => 1,
            Breakpoint.MdAndUp => 2,
            Breakpoint.LgAndUp => 3,
            Breakpoint.XlAndUp => 4,
            _ => int.MaxValue,
        };
    }
}
