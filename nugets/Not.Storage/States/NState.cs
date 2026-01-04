namespace Not.Storage.States;

public abstract class NState : IState
{
    Guid? IState.TransactionId { get; set; }
}
