using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Events;

namespace Not.Application.Behinds;

public interface ICrudeParent<T> : ICreate<T>, IUpdate<T>, IDeleteMany<T>, ICrudeParentContext
    where T : AggregateRoot
{
    IReadOnlyList<T> Children { get; }
    Event Changed { get; }
}
