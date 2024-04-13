using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Enums;

namespace EMS.Witness.Rpc;

public class ParticipantsClient : RpcClient, IParticipantsClient, IParticipantsClientProcedures
{
    private readonly SignalRSocket _socket;

    public event EventHandler<(ParticipantEntry entry, CollectionAction action)>? Updated;
	public event EventHandler<IEnumerable<ParticipantEntry>>? Loaded;

	public ParticipantsClient(SignalRSocket socket) : base(socket)
    {
        _socket = socket;
        RegisterClientProcedure<ParticipantEntry, CollectionAction>(nameof(this.ReceiveEntryUpdate), this.ReceiveEntryUpdate);
    }

    public Task ReceiveEntryUpdate(ParticipantEntry entry, CollectionAction action)
    {
        this.Updated?.Invoke(this, (entry, action));
        return Task.CompletedTask;
    }

    public async Task<RpcInvokeResult<ParticipantsPayload>> Load()
	{
		return await InvokeHubProcedure<ParticipantsPayload>(nameof(IParticipantstHubProcedures.SendParticipants));
	}

    public async Task<RpcInvokeResult> Send(IEnumerable<ParticipantEntry> entries, WitnessEventType type)
    {
		return await InvokeHubProcedure(nameof(IParticipantstHubProcedures.ReceiveWitnessEvent), entries, type);
    }
}

public interface IParticipantsClient
{
	event EventHandler<(ParticipantEntry entry, CollectionAction action)>? Updated;
	Task<RpcInvokeResult<ParticipantsPayload>> Load();
	Task<RpcInvokeResult> Send(IEnumerable<ParticipantEntry> entries, WitnessEventType type);
}
