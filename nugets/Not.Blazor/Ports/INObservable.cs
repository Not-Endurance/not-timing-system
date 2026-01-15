using Not.Injection;

namespace Not.Blazor.Ports;

public interface INObservable
{
    Task Initialize(params IEnumerable<object> arguments);
    void Subscribe(Func<Task> action);
}
