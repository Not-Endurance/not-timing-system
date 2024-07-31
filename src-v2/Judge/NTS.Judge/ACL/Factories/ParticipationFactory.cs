﻿using NTS.Compatibility.EMS.Entities.Participants;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Judge.ACL.Bridge;
using EmsParticipation = NTS.Compatibility.EMS.Entities.Participations.EmsParticipation;
using EmsCompetition = NTS.Compatibility.EMS.Entities.Competitions.EmsCompetition;
using EmsCompetitionType = NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType;
using NTS.Domain.Enums;
using NTS.Domain;
using NTS.Compatibility.EMS.Entities.Results;
using NTS.Compatibility.EMS.Entities.LapRecords;

namespace NTS.Judge.ACL.Factories;

public class ParticipationFactory
{
    public static EmsParticipation CreateEms(Participation participation)
    {
        var athlete = AthleteFactory.Create(participation);
        var horse = HorseFactory.Create(participation);
        
        var state = new EmsParticipantState
        {
            Number = participation.Tandem.Number.ToString(),
            MaxAverageSpeedInKmPh = (int)participation.Tandem.MinAverageSpeed!,
            Unranked = true // TODO: fix when Unranked is added on Unranked level
        };
        var emsParticipant = new EmsParticipant(athlete, horse, state);
        var emsLaps = LapFactory.Create(participation).ToList();
        for (var i = 0; i < participation.Phases.Count; i++)
        {
            var phase = participation.Phases[i];
            var emsLap = emsLaps[i];
            if (phase.StartTime == null)
            {
                break;
            }
            var emsRecord = new EmsLapRecord(phase.StartTime.DateTime.DateTime, emsLap);
            emsParticipant.Add(emsRecord);
        }
        var competition = CompetitionFactory.Create(participation);

        return new EmsParticipation(emsParticipant, competition);
    }

    public static Participation CreateCore(EmsParticipation emsParticipation, EmsCompetition competition)
    {
        var tandem = new Tandem(
            int.Parse(emsParticipation.Participant.Number),
            new Person(emsParticipation.Participant.Athlete.Name),
            emsParticipation.Participant.Horse.Name,
            competition.Laps.Sum(x => (decimal)x.LengthInKm),
            null,
            null,
            12,
            emsParticipation.Participant.MaxAverageSpeedInKmPh);
        
        var phases = new List<Phase>();
        EmsLapRecord? finalRecord = null;
        foreach (var lap in competition.Laps)
        {
            var type = EmsCompetitionTypeToCompetitionType(competition.Type);
            var phase = new Phase(
                lap.LengthInKm,
                lap.MaxRecoveryTimeInMins,
                lap.RestTimeInMins,
                type,
                lap.IsFinal, 
                null);
            var record = emsParticipation.Participant.LapRecords.FirstOrDefault(x => x.Lap == lap);
            
            if (record != null)
            {
                if (record.ArrivalTime.HasValue)
                {
                    phase.ArriveTime = new Timestamp(record.ArrivalTime.Value);
                }
                if (record.InspectionTime.HasValue)
                {
                    phase.InspectTime = new Timestamp(record.InspectionTime.Value);
                }
                if (record.ReInspectionTime.HasValue)
                {
                    phase.ReinspectTime = new Timestamp(record.ReInspectionTime.Value);
                }
                phase.StartTime = new Timestamp(record.StartTime);
                phase.IsReinspectionRequested = record.IsRequiredInspectionRequired;
                phase.IsCRIRequested = record.IsRequiredInspectionRequired;
                finalRecord = record;
            }

            phases.Add(phase);
        }

        var participation = new Participation(competition.Name, tandem, phases);
        if (finalRecord?.Result?.Type == EmsResultType.FailedToQualify)
        {
            participation.FailToQualify(FTQCodes.GA);
        }
        if (finalRecord?.Result?.Type == EmsResultType.Resigned)
        {
            participation.Retire();
        }
        if (finalRecord?.Result?.Type == EmsResultType.Disqualified)
        {
            participation.Disqualify(finalRecord.Result.Code);
        }
        return participation;
    }

    private static CompetitionType EmsCompetitionTypeToCompetitionType(EmsCompetitionType emsCompetitionType)
    {
        return emsCompetitionType switch
        {
            EmsCompetitionType.National => CompetitionType.National,
            EmsCompetitionType.International => CompetitionType.FEI,
            _ => throw new NotImplementedException(),
        };
    }
}