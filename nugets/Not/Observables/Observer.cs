namespace Not.Observables;

public abstract class Observer : IDisposable
{
    readonly Dictionary<Guid, IObservable> _observables = [];

    protected void Observe(IObservable observable, Action action)
    {
        var id = observable.Event.Subscribe(action);
        _observables.Add(id, observable);
    }

    public void Dispose()
    {
        foreach (var (id, observable) in _observables)
        {
            observable.Event.Unsubscribe(id);
        }
        GC.SuppressFinalize(this);
    }
}
