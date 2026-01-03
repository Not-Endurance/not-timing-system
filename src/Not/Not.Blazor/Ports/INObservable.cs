using Not.Injection;

namespace Not.Blazor.Ports;

public interface INObservable : ISingleton
{
    Task Initialize(params IEnumerable<object> arguments);
    void Subscribe(Func<Task> action);
}
