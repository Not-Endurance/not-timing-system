using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Enums;
using static Core.Application.CoreApplicationConstants;

namespace EMS.Witness.Rpc;

public class ParticipantsClient: RpcClient, IParticipantsClient, IParticipantsClientProcedures
{
	public event EventHandler<(ParticipantEntry entry, CollectionAction action)>? Updated;
	public event EventHandler<IEnumerable<ParticipantEntry>>? Loaded;

	public ParticipantsClient()
		: base(new RpcContext(RpcProtocls.Http, NetworkPorts.JUDGE_SERVER, RpcEndpoints.PARTICIPANTS))
    {
		this.RegisterClientProcedure<ParticipantEntry, CollectionAction>(nameof(this.Update), this.Update);
	}

    public Task Update(ParticipantEntry entry, CollectionAction action)
    {
        this.Updated?.Invoke(this, (entry, action));
        return Task.CompletedTask;
    }

    public async Task<RpcInvokeResult<ParticipantsPayload>> Load()
	{
		return await this.InvokeHubProcedure<ParticipantsPayload>(nameof(IParticipantstHubProcedures.Get));
	}

    public async Task<RpcInvokeResult> Send(IEnumerable<ParticipantEntry> entries, WitnessEventType type)
    {
		return await this.InvokeHubProcedure(nameof(IParticipantstHubProcedures.Witness), entries, type);
    }
}

public interface IParticipantsClient : IRpcClient
{
	event EventHandler<(ParticipantEntry entry, CollectionAction action)>? Updated;
	Task<RpcInvokeResult<ParticipantsPayload>> Load();
	Task<RpcInvokeResult> Send(IEnumerable<ParticipantEntry> entries, WitnessEventType type);
}
