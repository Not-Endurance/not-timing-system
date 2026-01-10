using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Events;

namespace Not.Application.Krud;

public interface IKrudParentNodeOf<T> : ICreate<T>, IUpdate<T>, IDeleteMany<T>, IKrudNodeSetter
    where T : AggregateRoot
{
    IReadOnlyList<T> Children { get; }
    Event Changed { get; } // TODO: convert to IObservable
}
