using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using EMS.Judge.Api.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc.Hubs;

public class ParticipantsHub : Hub<IParticipantsClientProcedures>, IParticipantstHubProcedures
{
    private readonly ManagerRoot managerRoot;
    
    public ParticipantsHub(IJudgeServiceProvider provider)
    {
        this.managerRoot = provider.GetRequiredService<ManagerRoot>();
    }

    public ParticipantsPayload Get()
    {
        var participants = this.managerRoot.GetActiveParticipants();
        var eventId = managerRoot.GetEventId();
        return new ParticipantsPayload
        {
            Participants = participants.ToList(),
            EventId = eventId,
        };
    }
    public Task Witness(IEnumerable<ParticipantEntry> entries, WitnessEventType type)
    {
        Task.Run(() =>
        {
            foreach (var entry in entries)
            {
                var witnessEvent = new WitnessEvent
                {
                    TagId = entry.Number,
                    Time = entry.ArriveTime!.Value,
                    Type = type,
                    IsFromWitnessApp = true,
                };
                Core.Domain.AggregateRoots.Manager.WitnessEvents.Witness.Raise(witnessEvent);
            }
        });
        
        return Task.CompletedTask;
    }
}
