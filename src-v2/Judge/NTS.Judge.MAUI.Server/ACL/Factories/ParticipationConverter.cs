﻿using NTS.Compatibility.EMS.Entities.Participants;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Judge.MAUI.Server.ACL.Bridge;
using EmsParticipation = NTS.Compatibility.EMS.Entities.Participations.EmsParticipation;
using EmsCompetition = NTS.Compatibility.EMS.Entities.Competitions.EmsCompetition;
using EmsCompetitionType = NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType;
using NTS.Domain.Enums;
namespace NTS.Judge.MAUI.Server.ACL.Factories;

public class ParticipationConverter
{
    public static EmsParticipation Create(Participation participation)
    {
        var athlete = EmsAthleteFactory.Create(participation);
        var horse = EmsHorseFactory.Create(participation);
        
        var state = new EmsParticipantState
        {
            Number = participation.Tandem.Number.ToString(),
            MaxAverageSpeedInKmPh = (int)participation.Tandem.MinAverageSpeed!,
            Unranked = true // TODO: fix when Unranked is added on Unranked level
        };
        var participant = new EmsParticipant(athlete, horse, state);
        var competition = EmsCompetitionFactory.Create(participation);

        return new EmsParticipation(participant, competition);
    }

    public static Participation Create(EmsParticipation emsParticipation, EmsCompetition competition)
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
            phases.Add(phase);
        }
        return new Participation(competition.Name, tandem, phases);
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
