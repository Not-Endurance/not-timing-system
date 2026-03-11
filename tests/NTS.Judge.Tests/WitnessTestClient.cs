using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Tests.RPC;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using Xunit.Abstractions;

namespace NTS.Judge.Tests;

public class WitnessTestClient : RpcClient, IWitnessClientProcedures, ITestRpcClient, ISingleton
{
    public WitnessTestClient(IRpcSocket socket, ITestOutputHelper _)
        : base(socket)
    {
        RegisterInputProcedure<ParticipationEliminated>(nameof(OnParticipationEliminated), OnParticipationEliminated);
        RegisterInputProcedure<ParticipationRestored>(nameof(OnParticipationRestored), OnParticipationRestored);
        RegisterInputProcedure<PhaseCompleted>(nameof(OnPhaseCompleted), OnPhaseCompleted);
    }

    public int Id { get; }
    public List<string> InvokedMethods { get; } = [];

    public void Dispose()
    {
        // Reset the invoked methods after each test
        InvokedMethods.Clear();
    }

    public Task OnParticipationEliminated(ParticipationEliminated payload)
    {
        InvokedMethods.Add(nameof(OnParticipationEliminated));
        return Task.CompletedTask;
    }

    public Task OnParticipationRestored(ParticipationRestored payload)
    {
        InvokedMethods.Add(nameof(OnParticipationRestored));
        return Task.CompletedTask;
    }

    public Task OnPhaseCompleted(PhaseCompleted payload)
    {
        InvokedMethods.Add(nameof(OnPhaseCompleted));
        return Task.CompletedTask;
    }

    public override void RunAtStartup()
    {
        throw new NotImplementedException();
    }
}


