using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Application.Services;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Enums;
using static Core.Application.CoreApplicationConstants;

namespace EMS.Witness.Rpc;

public class StartlistClient : RpcClient, IStartlistClientProcedures, IStartlistClient 
{
	public event EventHandler<(StartlistEntry entry, CollectionAction action)>? Updated;
 
    public StartlistClient(IHandshakeService handshakeService)
        : base(new RpcContext(RpcProtocls.Http, NetworkPorts.JUDGE_SERVER, RpcEndpoints.STARTLIST))
    {
        this.RegisterClientProcedure<StartlistEntry, CollectionAction>(nameof(this.Update), this.Update);
	}

    public Task Update(StartlistEntry entry, CollectionAction action)
	{
		this.Updated?.Invoke(this, (entry, action));
		return Task.CompletedTask;
	}

	public async Task<RpcInvokeResult<Dictionary<int, Startlist>>> Load()
	{
		return await this.InvokeHubProcedure<Dictionary<int, Startlist>>(nameof(IStartlistHubProcedures.Get));
    }
}

public interface IStartlistClient : IRpcClient
{
    event EventHandler<(StartlistEntry entry, CollectionAction action)>? Updated;
    Task<RpcInvokeResult<Dictionary<int, Startlist>>> Load();
}