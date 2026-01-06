using Not.Domain.Aggregates;

namespace Not.Storage.JsonFile.States;

public interface ITreeState<T> : IState
    where T : AggregateRoot
{
    T? Root { get; set; }
}
