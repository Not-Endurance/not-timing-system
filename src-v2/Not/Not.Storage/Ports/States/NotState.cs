
namespace Not.Storage.Ports.States;

public abstract class NotState : IState
{
    Guid? IState.TransactionId { get; set ; }
}
