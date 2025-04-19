using Not.Blazor.CRUD.Ports;
using Not.Domain.Base;
using Not.Events;

namespace Not.Application.Behinds;

public interface ICrudeParent<T> : ICrudePropagator<T>, ICrudeParentContext
    where T : AggregateRoot
{
    IReadOnlyList<T> Children { get; }
    Event Changed { get; }
    Task Add(T child);
    Task Remove(params IEnumerable<T> children);
}
