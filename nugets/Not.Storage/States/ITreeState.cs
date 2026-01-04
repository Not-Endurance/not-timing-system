using Not.Domain.Aggregates;

namespace Not.Storage.States;

public interface ITreeState<T> : IState
    where T : AggregateRoot
{
    T? Root { get; set; }
}
