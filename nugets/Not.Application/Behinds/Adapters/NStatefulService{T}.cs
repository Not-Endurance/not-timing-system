using Not.Observables;

namespace Not.Application.Behinds.Adapters;

public abstract class NStatefulService<T> : NStatefulService, IDisposable
    where T : IObservable, new()
{
    protected NStatefulService(T observable)
    {
        State = observable;
        Observe(observable, EmitChanged);
    }

    protected NStatefulService()
        : this(new T()) { }

    protected T State { get; }

    protected override void ClearState()
    {
        base.ClearState();
    }
}
