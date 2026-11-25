using Microsoft.AspNetCore.SignalR;
using NoTiming.Application.Contracts.Notices;
using NoTiming.EventBus.Contracts.Features.Judge.Procedures;
using NoTiming.EventBus.Contracts.Features.Ui.Procedures;
using NoTiming.EventBus.Features.Judge;
using NoTiming.EventBus.Features.Witness;

namespace NoTiming.EventBus.Outbox;

/// <summary>Sends a participation change to every Judge and Ui connected to the event.</summary>
internal sealed class ParticipationChangedPublisher
{
    readonly IHubContext<JudgeRpcHub, IJudgeClientProcedures> _judges;
    readonly IHubContext<WitnessRpcHub, IUiClientProcedures> _witnesses;

    public ParticipationChangedPublisher(
        IHubContext<JudgeRpcHub, IJudgeClientProcedures> judges,
        IHubContext<WitnessRpcHub, IUiClientProcedures> witnesses
    )
    {
        _judges = judges;
        _witnesses = witnesses;
    }

    public Task Publish(int eventId, ParticipationChangedNotice notice)
    {
        var group = eventId.ToString();
        return Task.WhenAll(
            _judges.Clients.Group(group).ParticipationChanged(notice),
            _witnesses.Clients.Group(group).ParticipationChanged(notice)
        );
    }
}
