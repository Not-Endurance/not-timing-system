using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Tests.RPC;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.RPC.Procedures;
using Xunit.Abstractions;

namespace NTS.Judge.Tests;

public class WitnessTestClient
    : RpcClient,
        IEmsParticipantsClientProcedures,
        IEmsStartlistClientProcedures,
        ITestRpcClient
{
    private readonly IRpcSocket _socket;
    private readonly ITestOutputHelper _testOutputHelper;

    public WitnessTestClient(IRpcSocket socket, ITestOutputHelper testOutputHelper)
        : base(socket)
    {
        _socket = socket;
        _testOutputHelper = testOutputHelper;
        RegisterClientProcedure<EmsStartlistEntry, EmsCollectionAction>(nameof(ReceiveEntry), ReceiveEntry);
        RegisterClientProcedure<EmsParticipantEntry, EmsCollectionAction>(
            nameof(ReceiveEntryUpdate),
            ReceiveEntryUpdate
        );
    }

    public int Id { get; }
    public List<string> InvokedMethods { get; } = [];

    public void Dispose()
    {
        // Reset the invoked methods after each test
        InvokedMethods.Clear();
    }

    public Task ReceiveEntry(EmsStartlistEntry entry, EmsCollectionAction action)
    {
        InvokedMethods.Add(nameof(ReceiveEntry));
        return Task.CompletedTask;
    }

    public Task ReceiveEntryUpdate(EmsParticipantEntry entry, EmsCollectionAction action)
    {
        InvokedMethods.Add(nameof(ReceiveEntryUpdate));
        return Task.CompletedTask;
    }

    public override void RunAtStartup()
    {
        throw new NotImplementedException();
    }
}
