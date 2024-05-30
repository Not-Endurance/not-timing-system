namespace Not.Storage.Ports.States;

public interface ITreeState<T> : IState
    where T : DomainEntity
{
    T? Root { get; set; }
}
