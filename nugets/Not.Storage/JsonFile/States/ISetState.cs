using Not.Domain.Aggregates;

namespace Not.Storage.JsonFile.States;

public interface ISetState<T> : IState
    where T : AggregateRoot
{
    List<T> EntitySet { get; }
}
