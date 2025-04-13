using Not.Application.CRUD.Ports;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.RPC;

public class EnduranceEventRpcClient : RpcClient, IEnduranceEventRpcClient
{
    readonly IRead<EnduranceEvent> _eventReader;

    public EnduranceEventRpcClient(IRpcSocket socket, IRead<EnduranceEvent> eventReader) : base(socket)
    {
        _eventReader = eventReader;
    }

    public override void RunAtStartup()
    {
        RegisterClientProcedure(nameof(GetEventId), GetEventId);
    }

    public async Task<int?> GetEventId()
    {
        var enduranceEvent = await _eventReader.Read(0);
        return enduranceEvent?.Id;
    }
}

public interface IEnduranceEventRpcClient : IRpcClient
{
    Task<int?> GetEventId();
}
