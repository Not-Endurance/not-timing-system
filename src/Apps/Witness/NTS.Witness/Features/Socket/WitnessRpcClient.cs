using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Injection;
using NTS.Contracts.Features.Witness.Procedures;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Features.Socket;

public class WitnessRpcClient : RpcClient, IWitnessClientProcedures, ISnapshotPublisher, IScoped
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessRpcClient(IRpcSocket socket, IDomainEventDispatcher domainEventDispatcher)
        : base(socket)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override void RegisterProcedures()
    {
        RegisterInputProcedure<ParticipationArrived>(nameof(OnParticipationArrived), OnParticipationArrived);
        RegisterInputProcedure<InspectionRequired>(nameof(OnInspectionRequired), OnInspectionRequired);
        RegisterInputProcedure<RepresentationRequired>(nameof(OnRepresentationRequired), OnRepresentationRequired);
        RegisterInputProcedure<PhaseCompleted>(nameof(OnPhaseCompleted), OnPhaseCompleted);
        RegisterInputProcedure<ParticipationEliminated>(nameof(OnParticipationEliminated), OnParticipationEliminated);
        RegisterInputProcedure<ParticipationRestored>(nameof(OnParticipationRestored), OnParticipationRestored);
    }

    public Task PublishSnapshotsAsync(SnapshotGroup snapshotGroup)
    {
        // The hub has no write path (ADR-0013). Snapshots are POSTed to the Api once the server records them (#644).
        throw new NotSupportedException("Snapshots cannot be sent until the server records them.");
    }

    public Task OnPhaseCompleted(PhaseCompleted payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnParticipationArrived(ParticipationArrived payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnInspectionRequired(InspectionRequired payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnRepresentationRequired(RepresentationRequired payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnParticipationEliminated(ParticipationEliminated payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnParticipationRestored(ParticipationRestored payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }
}
