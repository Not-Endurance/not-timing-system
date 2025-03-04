using Not.Blazor.Ports;
using Not.Domain.Base;
using Not.Structures;

namespace Not.Application.Behinds;

public interface IParentContext<T> : IParentContext
    where T : AggregateRoot
{
    Task Persist(); // TODO: consider removing persist and changing Add/Update/Remove to task
    ObservableList<T> Children { get; }
    void Add(T child);
    void Update(T child);
    void Remove(T child);
}
