using Not.Application.CRUD.Ports;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Domain.Setup.Aggregates;
using NTS.Warp.Features.Judge.Procedures;

namespace NTS.Judge.RPC;

public class EnduranceEventClientProcedures : RpcClient, IEnduranceEventClientProcedures
{
    readonly IRead<EnduranceEvent> _eventReader;

    public EnduranceEventClientProcedures(IRpcSocket socket, IRead<EnduranceEvent> eventReader)
        : base(socket)
    {
        _eventReader = eventReader;
    }

    public override void RunAtStartup()
    {
        RegisterOutputProcedure(nameof(GetEventId), GetEventId);
    }

    public async Task<int?> GetEventId()
    {
        var enduranceEvent = await _eventReader.Read(0);
        return enduranceEvent?.Id;
    }
}
