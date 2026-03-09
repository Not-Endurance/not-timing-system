using Not.Application.Behinds.Adapters;
using Not.Observables;

namespace Not.Blazor.Components.Abstractions;

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

    public virtual void Dispose()
    {
        foreach (var (id, observable) in _subscriptions)
        {
            observable.ObservableEvent.Unsubscribe(id);
        }
        GC.SuppressFinalize(this);
    }

    void InternalObserve(IObservable observable)
    {
        var id = observable.ObservableEvent.Subscribe(InvokeRender);
        _subscriptions.Add(id, observable);
    }
}
