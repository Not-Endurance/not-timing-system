namespace Not.Storage.States;

public interface IState
{
    Guid? TransactionId { get; internal set; }
}
