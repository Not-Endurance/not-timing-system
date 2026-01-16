using Not.Events;

namespace Not.Observables;

public interface IObservable
{
    IEventSubscriber Event { get; }
}
