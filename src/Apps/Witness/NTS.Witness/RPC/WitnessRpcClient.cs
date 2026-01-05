using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Collections;
using NTS.Application.Services;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Witness.RPC.Procedures;
using NTS.Witness.Services;

namespace NTS.Witness.RPC;

public class WitnessRpcClient : RpcClient, IWitnessParticipantsClientProcedures, IWitnessStartlistClientProcedures
{
    readonly IRpcSocket _socket;
    readonly IStartlistContext _startlistContext;
    readonly ParticipationService _participationService;

    public WitnessRpcClient(
        IRpcSocket socket,
        ParticipationService participationService,
        IStartlistContext startlistContext
    )
        : base(socket)
    {
        _socket = socket;
        _startlistContext = startlistContext;
        _participationService = participationService;
    }

    public override void RunAtStartup()
    {
        RegisterInputProcedure<StartlistEntry, NCollectionAction>(nameof(Receive), Receive);
        RegisterInputProcedure<Participation, NCollectionAction>(nameof(Receive), Receive);
    }

    public async Task<RpcInvokeResult> PublishSnapshotsAsync(WitnessSnapshotPayload payload)
    {
        return await _socket.InvokeInputProcedure(nameof(IWitnessHubProcedures.Receive), payload);
    }

    public Task Receive(StartlistEntry entry, NCollectionAction action)
    {
        _startlistContext.Update(entry, action);
        return Task.CompletedTask;
    }

    public Task Receive(Participation participation, NCollectionAction action)
    {
        _participationService.Update(participation, action);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Participation>> GetParcipations()
    {
        var participations = DummyData.CreateParticipations(10);
        return Task.FromResult(participations.AsEnumerable());
    }
}
