using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Enums;

namespace EMS.Witness.Rpc;

public class StartlistClient : RpcClient, IStartlistClientProcedures, IStartlistClient 
{
    public event EventHandler<(StartlistEntry entry, CollectionAction action)>? Updated;

    public StartlistClient(SignalRSocket socket) : base(socket)
    {
        RegisterClientProcedure<StartlistEntry, CollectionAction>(nameof(this.ReceiveEntry), this.ReceiveEntry);
    }

    public Task ReceiveEntry(StartlistEntry entry, CollectionAction action)
	{
		Updated?.Invoke(this, (entry, action));
		return Task.CompletedTask;
	}

	public async Task<RpcInvokeResult<Dictionary<int, Startlist>>> Load()
	{
		return await InvokeHubProcedure<Dictionary<int, Startlist>>(nameof(IStartlistHubProcedures.SendStartlist));
    }
}

public interface IStartlistClient
{
    event EventHandler<(StartlistEntry entry, CollectionAction action)>? Updated;
    Task<RpcInvokeResult<Dictionary<int, Startlist>>> Load();
}