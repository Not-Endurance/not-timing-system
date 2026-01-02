using Not.Domain.Aggregates;

namespace Not.Storage.States;

public interface ISetState<T> : IState
    where T : AggregateRoot
{
    List<T> EntitySet { get; }
}
