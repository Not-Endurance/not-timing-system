using Not.Async;
using Not.Notify;
using Not.Safe;
using Not.Strings;

namespace Not.Blazor.Components.Abstractions;

public class NComponent : NComponentBase
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
                Notifier.Error(ex);
            }
        });
    }

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected Task InvokeRender()
    {
        return _coalescedRender.Invoke();
    }

    protected virtual void OnBeforeRender() { }

    protected virtual Task OnBeforeRenderAsync()
    {
        return Task.CompletedTask;
    }

    protected void Handle(Exception ex)
    {
        SafeHelper.HandleException(ex);
    }
}
