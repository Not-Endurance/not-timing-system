using NC.Application.Identity;
using NC.Application.RPC;
using NC.Application.RPC.Clients;
using NC.Injection;
using NoTiming.Application.Contracts.Notices;
using NoTiming.Judge.Features.Core;

namespace NoTiming.Judge.Features.Socket;

/// <summary>Receives participation change notices. The change itself is fetched by <see cref="JudgeParticipationService"/>.</summary>
public class JudgeRpcClient : RpcClient, IScoped
{
    readonly JudgeParticipationService _participations;

    public JudgeRpcClient(IRpcSocket socket, IClientIdentity identity, JudgeParticipationService participations)
        : base(socket, identity)
    {
        _participations = participations;
    }

    protected override void RegisterProcedures()
    {
        RegisterChangeProcedure<ParticipationChangedNotice>("ParticipationChanged", OnParticipationChanged);
    }

    Task OnParticipationChanged(ParticipationChangedNotice notice, bool isRemote)
    {
        // This judge applies its own commands as soon as they return, so its own notices need no second fetch.
        return isRemote ? _participations.Refresh(notice.ParticipationId, isRemote) : Task.CompletedTask;
    }
}
