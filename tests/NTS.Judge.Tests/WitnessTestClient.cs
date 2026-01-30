using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Tests.RPC;
using NTS.Domain.Core.Aggregates;
using NTS.Warp.Features.Witness.Procedures;
using Xunit.Abstractions;

namespace NTS.Judge.Tests;

public class WitnessTestClient : RpcClient, IWitnessClientProcedures, ITestRpcClient, ISingleton
{
    public WitnessTestClient(IRpcSocket socket, ITestOutputHelper _)
        : base(socket)
    {
        RegisterInputProcedure<Participation>(nameof(Receive), Receive);
    }

    public int Id { get; }
    public List<string> InvokedMethods { get; } = [];

    public void Dispose()
    {
        // Reset the invoked methods after each test
        InvokedMethods.Clear();
    }

    public Task Receive(Participation entry)
    {
        InvokedMethods.Add(nameof(Receive));
        return Task.CompletedTask;
    }

    public override void RunAtStartup()
    {
        throw new NotImplementedException();
    }
}
