using NC.Events;

namespace NC.Observables;

public interface IObservable
{
    IEventSubscriber ObservableEvent { get; }
}
