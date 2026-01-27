using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Collections;
using Not.Tests.RPC;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Warp.Features.Witness.Procedures;
using Xunit.Abstractions;

namespace NTS.Judge.Tests;

public class WitnessTestClient
    : RpcClient,
        IWitnessClientProcedures,
        ITestRpcClient
{
    public WitnessTestClient(IRpcSocket socket, ITestOutputHelper _)
        : base(socket)
    {
        RegisterInputProcedure<StartlistEntry, NCollectionAction>(nameof(ReceiveStartlistEntry), ReceiveStartlistEntry);
        RegisterInputProcedure<Participation, NCollectionAction>(
            nameof(ReceiveParticipation),
            ReceiveParticipation
        );
    }

    public int Id { get; }
    public List<string> InvokedMethods { get; } = [];

    public void Dispose()
    {
        // Reset the invoked methods after each test
        InvokedMethods.Clear();
    }

    public Task ReceiveStartlistEntry(StartlistEntry entry, NCollectionAction action)
    {
        InvokedMethods.Add(nameof(ReceiveStartlistEntry));
        return Task.CompletedTask;
    }

    public Task ReceiveParticipation(Participation entry, NCollectionAction action)
    {
        InvokedMethods.Add(nameof(ReceiveParticipation));
        return Task.CompletedTask;
    }

    public override void RunAtStartup()
    {
        throw new NotImplementedException();
    }
}
