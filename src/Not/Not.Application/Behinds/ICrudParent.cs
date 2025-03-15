using Not.Blazor.CRUD.Ports;
using Not.Domain.Base;
using Not.Structures;

namespace Not.Application.Behinds;

public interface ICrudParent<T> : ICrudParentContext
    where T : AggregateRoot
{
    ObservableList<T> Children { get; }
    Task Add(T child);
    Task Update(T child);
    Task Remove(T child);
}
