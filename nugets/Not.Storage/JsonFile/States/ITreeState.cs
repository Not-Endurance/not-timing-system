using Not.Domain;

namespace Not.Storage.JsonFile.States;

public interface ITreeState<T> : IState
    where T : Aggregate
{
    T? Root { get; set; }
}
