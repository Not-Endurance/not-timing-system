using Not.Blazor.Ports;
using Not.Domain.Base;
using Not.Structures;

namespace Not.Application.Behinds;

public interface IParentContext<T> : IParentContext
    where T : AggregateRoot
{
    ObservableList<T> Children { get; }
    Task Add(T child);
    Task Update(T child);
    Task Remove(T child);
}
