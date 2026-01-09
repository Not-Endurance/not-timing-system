namespace Not.Storage.JsonFile.States;

public abstract class NState : IState
{
    Guid? IState.TransactionId { get; set; }
}
