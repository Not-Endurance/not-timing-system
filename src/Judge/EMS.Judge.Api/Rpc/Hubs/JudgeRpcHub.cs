using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Domain.AggregateRoots.Manager.WitnessEvents;
using Core.Domain.State;
using Core.Enums;
using EMS.Judge.Api.Configuration;
using EMS.Judge.Application.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc.Hubs;

public class JudgeRpcHub : Hub<IClientProcedures>, IStartlistHubProcedures, IParticipantstHubProcedures
{
	private readonly ManagerRoot managerRoot;
    private readonly IPersistence _persistence;

    public JudgeRpcHub(IJudgeServiceProvider provider, IPersistence persistence)
	{
		this.managerRoot = provider.GetRequiredService<ManagerRoot>();
        _persistence = persistence;
    }
		
	public Dictionary<int, Startlist> SendStartlist()
	{
		var startlist = this.managerRoot.GetStartList();
		return startlist;
	}

    public ParticipantsPayload SendParticipants()
    {
        _persistence.Configure("../../../../event-archive/2023-10_asenovgrad");
        Console.WriteLine("Calling Get");
        var participants = this.managerRoot.GetActiveParticipants();
        var eventId = managerRoot.GetEventId();
        return new ParticipantsPayload
        {
            Participants = participants.ToList(),
            EventId = eventId,
        };
    }
    public Task ReceiveWitnessEvent(IEnumerable<ParticipantEntry> entries, WitnessEventType type)
    {
        // Task.Run because Event hadling in dotnet seems to hold the current thread. Further investigation is needed
        // but what was happening is that Witness apps didn't receive rpc response untill the handling thread was finished
        // which is motly visible when it causes a validation (popup) which blocks the thread until closed in Prism/WPF
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
                Witness.Raise(witnessEvent);
            }
        });

        return Task.CompletedTask;
    }

    public override Task OnConnectedAsync()
	{
		Console.WriteLine($"Connected: {this.Context.ConnectionId}");
        return Task.CompletedTask;
	}

	public override Task OnDisconnectedAsync(Exception exception)
	{
		Console.WriteLine($"Disconnected: {this.Context.ConnectionId}");
        return Task.CompletedTask;
    }

    public class ClientService : IDisposable
    {
        private readonly ManagerRoot _managerRoot;
        private readonly IHubContext<JudgeRpcHub, IClientProcedures> _hub;

        public ClientService(
            IHubContext<JudgeRpcHub, IClientProcedures> hub,
            IJudgeServiceProvider judgeServiceProvider)
        {
            _hub = hub;
            _managerRoot = judgeServiceProvider.GetRequiredService<ManagerRoot>();
            Witness.StartlistChanged += SendStartlistEntryUpdate;
            Witness.ParticipantChanged += SendParticipantEntryUpdate;
        }

        public void SendStartlistEntryUpdate(object? _, (string Number, CollectionAction Action) args)
        {
            var entry = this._managerRoot.GetStarlistEntry(args.Number);
            if (entry == null)
            {
                return;
            }
            _hub.Clients.All.ReceiveEntry(entry, args.Action);
        }

        public void SendParticipantEntryUpdate(object? _, (string Number, CollectionAction Action) args)
        {
            var entry = this._managerRoot.GetParticipantEntry(args.Number);
            if (entry == null)
            {
                return;
            }
            _hub.Clients.All.ReceiveEntryUpdate(entry, args.Action);
        }

        public void Dispose()
        {
            Witness.StartlistChanged -= SendStartlistEntryUpdate;
            Witness.ParticipantChanged -= SendParticipantEntryUpdate;
        }
    }
}
