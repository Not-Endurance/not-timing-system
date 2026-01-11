using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Observables;

namespace Not.Application.Krud.Abstractions;

public interface IKrudParentNodeOf<T> : ICreate<T>, IUpdate<T>, IDeleteOne<T>, IKrudNodeSetter, IObservable
    where T : AggregateRoot
{
    IReadOnlyList<T> Children { get; }
}
