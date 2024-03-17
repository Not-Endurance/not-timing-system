using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Domain.State;
using EMS.Judge.Api.Configuration;
using EMS.Judge.Application.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc.Hubs;

public class ParticipantsHub : Hub<IParticipantsClientProcedures>, IParticipantstHubProcedures
{
    private readonly ManagerRoot managerRoot;
    private readonly IPersistence _persistence;
    private readonly IState _state;

    public ParticipantsHub(IJudgeServiceProvider provider)
    {
        this.managerRoot = provider.GetRequiredService<ManagerRoot>();
        _persistence = provider.GetRequiredService<IPersistence>();
        _state = provider.GetRequiredService<IState>();
    }

    public ParticipantsPayload Get()
    {
		_persistence.Configure("../../../../event-archive/2023-10_asenovgrad");
        Console.WriteLine($"State event (in hub): {_state.Event?.Id}, {_state.Event?.Name}");
		Console.WriteLine("Calling Get");
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
