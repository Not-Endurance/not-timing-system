using NC.Application.Behinds.Adapters;
using NC.Observables;

namespace NC.Blazor.Components.Abstractions;

public class NStatefulComponent : NComponent, IDisposable
{
    Dictionary<Guid, IObservable> _subscriptions = [];

    public bool IsLoading { get; protected set; } = true;

    protected async Task Observe(IStatefulService statefulService)
    {
        try
        {
            await statefulService.Load();
            IsLoading = false;
            await InvokeRender();
            InternalObserve(statefulService);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    public override void Dispose()
    {
        foreach (var (id, observable) in _subscriptions)
        {
            observable.ObservableEvent.Unsubscribe(id);
        }
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    void InternalObserve(IObservable observable)
    {
        var id = observable.ObservableEvent.Subscribe(InvokeRender);
        _subscriptions.Add(id, observable);
    }
}
