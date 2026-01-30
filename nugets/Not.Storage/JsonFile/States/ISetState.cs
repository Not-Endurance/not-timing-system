using Not.Domain;

namespace Not.Storage.JsonFile.States;

public interface ISetState<T> : IState
    where T : Aggregate
{
    List<T> EntitySet { get; }
}
