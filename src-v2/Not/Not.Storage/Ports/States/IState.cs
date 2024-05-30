namespace Not.Storage.Ports.States;

public interface IState
{
    Guid? TransactionId { get; internal set; }
}
