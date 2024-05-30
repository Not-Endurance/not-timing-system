namespace Not.Storage.Ports.States;

public interface ISetState<T> : IState
    where T : DomainEntity
{
    List<T> EntitySet { get; }
}
