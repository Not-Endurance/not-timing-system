using Not.Observables;

namespace Not.Blazor.Ports;

public interface IStatefulService : IObservable
{
    Task Initialize(params IEnumerable<object> arguments);
}
