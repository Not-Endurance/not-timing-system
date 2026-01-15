namespace Not.Storage.JsonFile.States;

public interface IState
{
    Guid? TransactionId { get; internal set; }
}
