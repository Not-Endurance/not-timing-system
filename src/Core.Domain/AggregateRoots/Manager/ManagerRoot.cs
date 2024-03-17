using Core.Domain.AggregateRoots.Common.Performances;
using Core.Domain.AggregateRoots.Manager.Aggregates;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Domain.AggregateRoots.Manager.WitnessEvents;
using Core.Domain.Common.Exceptions;
using Core.Domain.Common.Models;
using Core.Domain.State;
using Core.Domain.State.Competitions;
using Core.Domain.State.LapRecords;
using Core.Domain.State.Participants;
using Core.Domain.State.Participations;
using Core.Enums;
using Core.Events;
using Core.Services;
using Core.Utilities;
using EMS.Judge.Application.Common.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Core.Localization.Strings;

namespace Core.Domain.AggregateRoots.Manager;

public class ManagerRoot : IAggregateRoot
{
    public static string dataDirectoryPath;
    private readonly IState state;
    private readonly IFileService file;
    private readonly IJsonSerializationService serialization;

    private static bool isRegisted;
    
    public ManagerRoot(IStateContext context)
    {
        // TODO: fix log
        this.file = StaticProvider.GetService<IFileService>();
        this.serialization = StaticProvider.GetService<IJsonSerializationService>();
        this.state = context.State;
        if (!isRegisted)
        {
            Witness.Events += this.Handle;
            isRegisted = true;
        }
    }

    private void Handle(object sender, WitnessEvent witnessEvent)
    {
        if (witnessEvent.TagId == "test")
        {
            return;
        }
        try
        {
            var number = this.GetParticipation(witnessEvent.TagId)?.Participant.Number;
            if (number == null)
            {
                return;
            }
            if (witnessEvent.Type == WitnessEventType.Arrival)
            {
                this.HandleArrive(witnessEvent);
            }
            if (witnessEvent.Type == WitnessEventType.VetIn)
            {
                this.HandleVet(witnessEvent);
            }
            Witness.RaiseStateChanged();
        }
        catch (Exception exception)
        {
            CoreEvents.RaiseError(exception);
        }
    }

    public StartlistEntry GetStarlistEntry(string number)
    {
        var participation = this.state.Participations.Find(x => x.Participant.Number == number);
        var entry = new StartlistEntry(participation);
        return entry;
    }

    public ParticipantEntry GetParticipantEntry(string number)
    {
        var participation = this.state.Participations.Find(x => x.Participant.Number == number);
        var entry = new ParticipantEntry(participation);
        return entry;
    }

    public bool HasStarted()
        => this.state.Participations.Any(x => x.Participant.LapRecords.Any());

    public void Start()
    {
        this.ValidateConfiguration();
        var participations = this.state
            .Participations
            .Select(x => new ParticipationsAggregate(x))
            .ToList();
        foreach (var participation in participations)
        {
            participation.Start();
            Witness.RaiseParticipantChanged(participation.Number, CollectionAction.AddOrUpdate);
        }
        this.state.Event.HasStarted = true;
    }

    public void UpdateRecord(string number, DateTime time)
    {
        var participation = this
            .GetParticipation(number)
            .Aggregate();
        var currentLap = participation.CurrentLap.Aggregate();
        if (currentLap.IsComplete || participation.CurrentLap.ArrivalTime == null)
        {
            participation.Arrive(time);
        }
        else
        {
            participation.Vet(time);
        }
    }

    private readonly Dictionary<string, DateTime> arrivalCache = new();
    private readonly Dictionary<string, DateTime> vetCache = new();

    // TODO: unify those methods
    public void HandleArrive(WitnessEvent witnessEvent)
    {
        var participation = this.GetParticipation(witnessEvent.TagId);
        var aggregate = participation.Aggregate();
        if (aggregate.CurrentLap.ArrivalTime != null && aggregate.CurrentLap.Result == null)
        {
            return;
        }
        // TODO: extract deduplication logic in common utility
        // Make sure that we only finish once even if we detect both tags
        var now = DateTime.Now;
        if (this.arrivalCache.ContainsKey(aggregate.Number)
            && now - this.arrivalCache[aggregate.Number] < TimeSpan.FromSeconds(30))
        {
            return;
        }
        this.arrivalCache[aggregate.Number] = now;
        aggregate.Arrive(witnessEvent.Time);
        if (witnessEvent is RfidTagEvent rfidTagEvent)
        {
            this.AddRfidDetectedEntry(participation, rfidTagEvent);
        }
    }
    public void HandleVet(WitnessEvent witnessEvent)
    {
        var participation = this.GetParticipation(witnessEvent.TagId);
        var aggregate = participation.Aggregate();
        if (aggregate.CurrentLap.Result != null
            || aggregate.CurrentLap.InspectionTime != null && aggregate.CurrentLap.ReInspectionTime != null)
        {
            return;
        }
        var now = DateTime.Now;
        if (this.vetCache.ContainsKey(aggregate.Number)
            && now - this.vetCache[aggregate.Number] < TimeSpan.FromSeconds(30))
        {
            return;
        }
        this.vetCache[aggregate.Number] = now;
        aggregate.Vet(witnessEvent.Time);
        if (witnessEvent is RfidTagEvent rfidTagEvent)
        {
            this.AddRfidDetectedEntry(participation, rfidTagEvent);
        }
    }

    private void AddRfidDetectedEntry(Participation participation, RfidTagEvent tagEvent)
    {
        var lastLap = participation.Participant.LapRecords.Last();
        if (!lastLap.Detected.ContainsKey(tagEvent.Type))
        {
            lastLap.Detected.Add(tagEvent.Type, tagEvent.Tag);
        }
        var index = participation.Participant.LapRecords.IndexOf(lastLap);
        participation.Participant.DetectedHead[tagEvent.Type].Add(index);
    }

    public void Disqualify(string number, string reason)
    {
        reason ??= nameof(DQ);
        var lap = this.GetLastLap(number);
        lap.Disqualify(number, reason);
    }
    public void FailToQualify(string number, string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            throw Helper.Create<ParticipantException>(PARTICIPANT_CANNOT_FTQ_WITHOUT_REASON_MESSAGE, FTQ);
        }
        var lap = this.GetLastLap(number);
        lap.FailToQualify(number, reason);
    }
    public void Resign(string number, string reason)
    {
        reason ??= nameof(RET);
        var lap = this.GetLastLap(number);
        lap.Resign(reason);
    }

    private LapRecordsAggregate GetLastLap(string participantNumber)
    {
        var participation = this.GetParticipation(participantNumber);
        var participationsAggregate = participation.Aggregate();
        var lap = participationsAggregate.CurrentLap;
        return lap.Aggregate();
    }

    public void RequireReInspection(string number, bool isRequired)
    {
        var participation = this.GetParticipation(number);
        var lastRecord = participation
            .Aggregate()
            .CurrentLap
            .Aggregate();
        lastRecord!.RequireReInspection(isRequired);
    }

    public void RequireCompulsoryInspection(string number, bool isRequired)
    {
        var participation = this.GetParticipation(number);
        var last = participation
            .Aggregate()
            .CurrentLap
            .Aggregate();
        last.RequireCompulsoryInspection(isRequired);
    }

    public Performance EditRecord(ILapRecordState state)
    {
        var participation = this.state
            .Participations
            .First(x => x.Participant.LapRecords.Contains(state));
        var records = participation.Participant.LapRecords.ToList();
        var record = records.First(rec => rec.Equals(state));
        var aggregate = new LapRecordsAggregate(record);
        aggregate.Edit(state);
        aggregate.CheckForResult(participation);
        participation.RaiseUpdate();
        var previousLength = records
            .Take(records.IndexOf(record))
            .Sum(x => x.Lap.LengthInKm);
        var performance = new Performance(record, participation.CompetitionConstraint.Type, previousLength);
        return performance;
    }

    public Dictionary<int, Startlist> GetStartList()
    {
        if (!this.state.Event.HasStarted)
        {
            return new();
        }
        var startlists = new Dictionary<int, Startlist>();
        var participants = this.state.Participations.Where(x => x.CompetitionConstraint != null).ToList();
        foreach (var participant in participants)
        {
            var toSkip = 0;
            foreach (var record in participant.Participant.LapRecords.Where(x => x.Result == null || !x.Result.IsNotQualified))
            {
                var entry = new StartlistEntry(participant, toSkip);
                if (startlists.ContainsKey(entry.Stage))
                {
                    startlists[entry.Stage].Add(entry);
                }
                else
                {
                    startlists.Add(entry.Stage, new Startlist(new List<StartlistEntry> { entry }));
                }
                // If record is complete, but is not last -> insert another record for current stage
                // This bullshit happens because "current" stage is not yet created. Its only created at Arrive
                if (record == participant.Participant.LapRecords.Last() && record.NextStarTime.HasValue)
                {
                    var nextEntry = new StartlistEntry(participant)
                    {
                        Stage = participant.Participant.LapRecords.Count + 1,
                        StartTime = record.NextStarTime.Value,
                    };
                    if (startlists.ContainsKey(nextEntry.Stage))
                    {
						startlists[nextEntry.Stage].Add(nextEntry);
					}
                    else
                    {
						startlists.Add(nextEntry.Stage, new Startlist(new List<StartlistEntry> { nextEntry }));
					}
				}
                toSkip++;
            }
        }
        return startlists;
    }

    public IEnumerable<ParticipantEntry> GetActiveParticipants()
    {
        var entries = this.state.Participations
            .Where(x => x.Participant.LapRecords.Any() && x.IsNotComplete)
            .Select(x => new ParticipantEntry(x));
        return entries;
    }

    public int GetEventId()
    {
        return state.Event.Id;
    }

    private Participation GetParticipation(string number)
    {
        if (string.IsNullOrEmpty(number))
        {
            return null;
        }
        var participation = this.state.Participations.FirstOrDefault(x => x.Participant.Number == number);
        return participation;
    }

    private void ValidateConfiguration()
    {
        var competitionWithoutLaps = this.state.Event.Competitions.FirstOrDefault(comp => comp.Laps.Count == 0);
        if (competitionWithoutLaps != null)
        {
            throw Helper.Create<CompetitionException>(
                COMPETITION_CANNOT_START_WITHOUT_PHASES_MESSAGES,
                competitionWithoutLaps.Name);
        }
        foreach (var competition in this.state.Event.Competitions)
        {
            if (competition.Laps.All(x => !x.IsFinal))
            {
                throw Helper.Create<CompetitionException>(
                    INVALID_COMPETITION_NO_FINAL_PHASE_MESSAGE,
                    competition.Name);
            }
        }
        foreach (var participation in this.state.Participations)
        {
            if (!participation.CompetitionsIds.Any())
            {
                throw Helper.Create<ParticipantException>(
                    INVALID_PARTICIPANT_NO_PARTICIPATIONS_MESSAGE,
                    participation.Participant.Number);
            }

            if (participation.Participant.Athlete.Country == null)
            {
                throw Helper.Create<ParticipantException>(
                    INVALID_PARTICIPANT_NO_COUNTRY_MESSAGE,
                    participation.Participant.Number);
            }
        }
    }
}
