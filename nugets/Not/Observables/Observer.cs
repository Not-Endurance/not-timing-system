namespace Not.Observables;

public abstract class Observer : IDisposable
{
    readonly Dictionary<Guid, IObservable> _observables = [];

    protected void Observe(IObservable observable, Action action)
    {
        var id = observable.Event.Subscribe(action);
        _observables.Add(id, observable);
    }

    protected void Observe(IObservable observable, Func<Task> action)
    {
        var id = observable.Event.Subscribe(action);
        _observables.Add(id, observable);
    }

    public virtual void Dispose()
    {
        foreach (var (id, observable) in _observables)
        {
            observable.Event.Unsubscribe(id);
        }
        _observables.Clear();
        GC.SuppressFinalize(this);
    }
}
