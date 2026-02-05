using Not.Application.Behinds.Adapters;
using Not.Observables;

namespace Not.Blazor.Components;

public class NStatefulComponent : NComponent, IDisposable
{
    Dictionary<Guid, IObservable> _subscriptions = [];

    public bool IsLoading { get; protected set; } = true;

    protected async Task Observe(IStatefulService statefulService)
    {
        await statefulService.Load();
        IsLoading = false;
        await InvokeRender();
        InternalObserve(statefulService);
    }

    public virtual void Dispose()
    {
        foreach (var (id, observable) in _subscriptions)
        {
            observable.Event.Unsubscribe(id);
        }
        GC.SuppressFinalize(this);
    }

    void InternalObserve(IObservable observable)
    {
        var id = observable.Event.Subscribe(InvokeRender);
        _subscriptions.Add(id, observable);
    }
}

// TODO: Delete
public class NStatefulComponent<T> : NStatefulComponent, IDisposable
    where T : IStatefulService
{
    [Inject]
    protected T Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
