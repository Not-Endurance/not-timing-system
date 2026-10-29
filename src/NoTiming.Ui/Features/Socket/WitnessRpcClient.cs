using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Injection;
using NTS.Contracts.Live;
using ParticipationChangedEvent = NTS.Domain.Core.Events.ParticipationChanged;

namespace NoTiming.Ui.Features.Socket;

/// <summary>
/// What the Api sends a viewer (ADR-0013): that a Participation changed. The store reads it again; nothing the hub
/// sends describes a Participation.
/// </summary>
public class WitnessRpcClient : RpcClient, ILiveClientProcedures, IScoped
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessRpcClient(IRpcSocket socket, IDomainEventDispatcher domainEventDispatcher)
        : base(socket)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override void RegisterProcedures()
    {
        RegisterInputProcedure<Guid, Guid>(nameof(ILiveClientProcedures.ParticipationChanged), ParticipationChanged);
    }

    /// <summary>
    /// Returns at once. The socket awaits a handler before it takes the next message, so a handler that waited for the
    /// read would put a burst of changes behind one read after another. The store reads each Participation once at a
    /// time, and a failure is reported through the error event of the socket.
    /// </summary>
    public Task ParticipationChanged(Guid eventId, Guid participationId)
    {
        _ = DispatchAsync(new ParticipationChangedEvent(eventId, participationId));
        return Task.CompletedTask;
    }

    async Task DispatchAsync(ParticipationChangedEvent change)
    {
        try
        {
            await _domainEventDispatcher.Dispatch(change);
        }
        catch (Exception exception)
        {
            Socket.RaiseError(
                exception,
                nameof(ILiveClientProcedures.ParticipationChanged),
                change.EventId,
                change.ParticipationId
            );
        }
    }
}
