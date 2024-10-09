using Microsoft.AspNetCore.SignalR;
using Not.Application.Ports.CRUD;
using Not.Concurrency;
using Not.Events;
using Not.Safe;
using NTS.Compatibility.EMS.Entities;
using NTS.Compatibility.EMS.Entities.EMS;
using NTS.Compatibility.EMS.Enums;
using NTS.Compatibility.EMS.RPC;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Events.Participations;
using NTS.Domain.Objects;
using NTS.Judge.ACL.Factories;
using NTS.Judge.Adapters.Behinds;
using NTS.Judge.Blazor.Ports;
using NTS.Judge.Ports;

namespace NTS.Judge.MAUI.Server.ACL;

public class EmsRpcHub : Hub<IEmsClientProcedures>, IEmsStartlistHubProcedures, IEmsEmsParticipantstHubProcedures
{
    private readonly IRepository<Participation> _participations;
    private readonly IRepository<Event> _events;
    private readonly IParticipationBehind _participationBehind;
    private readonly ISnapshotProcess _snapshotProcess;

    public EmsRpcHub(IJudgeServiceProvider judgeProvider)
    {
        _participations = judgeProvider.GetRequiredService<IRepository<Participation>>();
        _events = judgeProvider.GetRequiredService<IRepository<Event>>();
        _participationBehind = judgeProvider.GetRequiredService<IParticipationBehind>();
        _snapshotProcess = judgeProvider.GetRequiredService<ISnapshotProcess>();
    }

    public Dictionary<int, EmsStartlist> SendStartlist()
    {
        var participations = _participations.ReadAll().Result;
        var emsParticipations = participations
            .Select(ParticipationFactory.CreateEms);
        
        var startlists = new Dictionary<int, EmsStartlist>();
        foreach (var emsParticipation in emsParticipations)
        {
            var toSkip = 0;
            foreach (var record in emsParticipation.Participant.LapRecords.Where(x => x.Result == null || !x.Result.IsNotQualified))
            {
                var entry = new EmsStartlistEntry(emsParticipation, toSkip);
                if (startlists.ContainsKey(entry.Stage))
                {
                    startlists[entry.Stage].Add(entry);
                }
                else
                {
                    startlists.Add(entry.Stage, new EmsStartlist(new List<EmsStartlistEntry> { entry }));
                }
                // If record is complete, but is not last -> insert another record for current stage
                // This bullshit happens because "current" stage is not yet created. Its only created at Arrive
                if (record == emsParticipation.Participant.LapRecords.Last() && record.NextStarTime.HasValue)
                {
                    var nextEntry = new EmsStartlistEntry(emsParticipation)
                    {
                        Stage = emsParticipation.Participant.LapRecords.Count + 1,
                        StartTime = record.NextStarTime.Value,
                    };
                    if (startlists.ContainsKey(nextEntry.Stage))
                    {
                        startlists[nextEntry.Stage].Add(nextEntry);
                    }
                    else
                    {
                        startlists.Add(nextEntry.Stage, new EmsStartlist(new List<EmsStartlistEntry> { nextEntry }));
                    }
                }
                toSkip++;
            }
        }
        return startlists;
    }

    public EmsParticipantsPayload SendParticipants()
    {
        var participants = _participations
            .ReadAll(x => x.Phases.All(x => !x.IsComplete()))
            .Result
            .Select(ParticipantEntryFactory.Create);
        var @event = _events.Read(0);
        return new EmsParticipantsPayload
        {
            Participants = participants.ToList(),
            EventId = @event.Id,
        };
    }
    public Task ReceiveWitnessEvent(IEnumerable<EmsParticipantEntry> entries, EmsWitnessEventType type)
    {
        // Task.Run because Event hadling in dotnet seems to hold the current thread. Further investigation is needed
        // but what was happening is that Witness apps didn't receive rpc response untill the handling thread was finished
        // which is motly visible when it causes a validation (popup) which blocks the thread until closed in Prism/WPF
        SafeHelper.RunAsync(async () =>
        {
            foreach (var entry in entries)
            {
                var participation = await _participations.Read(x => x.Tandem.Number == int.Parse(entry.Number));
                if (participation == null)
                {
                    continue;
                }
                var isFinal = participation.Phases.Take(participation.Phases.Count - 1).All(x => x.IsComplete());
                var snapshot = SnapshotFactory.Create(entry, type, isFinal);
                await _snapshotProcess.Process(snapshot);
                //await _participationBehind.Process(snapshot);
            }
        });

        return Task.CompletedTask;
    }

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"Connected: {Context.ConnectionId}");
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        Console.WriteLine($"Disconnected: {Context.ConnectionId}");
        return Task.CompletedTask;
    }

    public class ClientService : IDisposable, IClientProcedures
    {
        private readonly IHubContext<EmsRpcHub, IEmsClientProcedures> _hub;

        public ClientService(
            IRepository<Participation> participations,
            IHubContext<EmsRpcHub, IEmsClientProcedures> hub)
        {
            _hub = hub;

            EventHelper.Subscribe<PhaseCompleted>(SendStartlistEntryUpdate);
            EventHelper.Subscribe<QualificationRestored>(SendParticipantEntryAddOrUpdate);
            EventHelper.Subscribe<QualificationRevoked>(SendParticipantEntryRemove);
        }

        public async void SendStartlistEntryUpdate(PhaseCompleted phaseCompleted)
        {
            var emsParticipation = ParticipationFactory.CreateEms(phaseCompleted.Participation);
            var entry = new EmsStartlistEntry(emsParticipation);
            await _hub.Clients.All.ReceiveEntry(entry, EmsCollectionAction.AddOrUpdate);
        }

        public async void SendParticipantEntryAddOrUpdate(QualificationRestored qualificationRestored)
        {
            var emsParticipation = ParticipationFactory.CreateEms(qualificationRestored.Participation);
            var entry = new EmsParticipantEntry(emsParticipation);
            await _hub.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.AddOrUpdate);
        }

        public async void SendParticipantEntryRemove(QualificationRevoked qualificationRevoked)
        {
            var emsParticipation = ParticipationFactory.CreateEms(qualificationRevoked.Participation);
            var entry = new EmsParticipantEntry(emsParticipation);
            await _hub.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.Remove);
        }

        public void Dispose()
        {
            // TODO: Dispose
        }

        public async Task ReceiveSnapshot(Snapshot snapshot)
        {
            await Task.CompletedTask;
        }

        public async Task SendStartCreated(PhaseCompleted phaseCompleted)
        {
            SendStartlistEntryUpdate(phaseCompleted);
            await Task.CompletedTask;
        }

        public async Task SendQualificationRevoked(QualificationRevoked revoked)
        {
            SendParticipantEntryRemove(revoked);
            await Task.CompletedTask;
        }

        public async Task SendQualificationRestored(QualificationRestored restored)
        {
            SendParticipantEntryAddOrUpdate(restored);
            await Task.CompletedTask;
        }
    }
}