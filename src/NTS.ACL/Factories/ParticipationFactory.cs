using Not.Strings;
using NTS.ACL.Entities.LapRecords;
using NTS.ACL.Entities.Participants;
using NTS.ACL.Entities.Results;
using NTS.ACL.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using EmsCompetition = NTS.ACL.Entities.Competitions.EmsCompetition;
using EmsParticipation = NTS.ACL.Entities.Participations.EmsParticipation;

namespace NTS.ACL.Factories;

public class ParticipationFactory
{
    public static EmsParticipation CreateEms(Participation participation)
    {
        var athlete = AthleteFactory.Create(participation);
        var horse = HorseFactory.Create(participation);

        var state = new EmsParticipantState { Number = participation.Combination.Number.ToString(), MaxAverageSpeedInKmPh = (int?)participation.Combination.MinAverageSpeed, Unranked = true }; // TODO: fix when Unranked is added on Unranked level
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
            var emsRecord = new EmsLapRecord(phase.StartTime.ToDateTime(), emsLap)
            {
                ArrivalTime = phase.ArriveTime?.ToDateTime(),
                InspectionTime = phase.PresentTime?.ToDateTime(),
                ReInspectionTime = phase.RepresentTime?.ToDateTime(),
            };
            emsParticipant.Add(emsRecord);
        }
        var competition = CompetitionFactory.Create(participation);

        return new EmsParticipation(emsParticipant, competition);
    }

    public static Participation CreateCore(
        EmsParticipation emsParticipation,
        EmsCompetition emsCompetition,
        bool adjustTime
    )
    {
        var minSpeed = emsParticipation.Participant.Athlete.Category == Enums.EmsCategory.Children ? 10 : 12;

        var combination = new Combination(
            int.Parse(emsParticipation.Participant.Number),
            emsParticipation.Participant.Athlete,
            emsParticipation.Participant.Horse,
            emsCompetition.Laps.Sum(x => (decimal)x.LengthInKm),
            ((IAthlete)emsParticipation.Participant.Athlete).Country,
            ((IAthlete)emsParticipation.Participant.Athlete).Club,
            minSpeed,
            emsParticipation.Participant.MaxAverageSpeedInKmPh
        );

        var phases = new List<Phase>();
        EmsLapRecord? finalRecord = null;
        DateTimeOffset? previousTime = null;
        foreach (var lap in emsCompetition.Laps)
        {
            var type = CompetitionFactory.MapCompetitionRuleset(emsCompetition.Type);

            var record = emsParticipation.Participant.LapRecords.FirstOrDefault(x => x.Lap == lap);

            Phase phase = null;
            if (record != null)
            {
                Timestamp? startTimestamp = null;
                if (phases.Any())
                {
                    // Adjusting isnt necessary since it's already done in the previous iteration
                    var outTime = phases.Last().GetOutTime();
                    previousTime = outTime?.ToDateTimeOffset();
                    startTimestamp = Timestamp.Copy(outTime);
                }
                else
                {
                    startTimestamp = new Timestamp(
                        AdjustTime(ref previousTime, record.StartTime, TimeSpan.Zero, adjustTime)
                    );
                }

                DateTime? arriveTime = null;
                DateTime? inspectTime = null;
                DateTime? reinspectTime = null;
                if (record.ArrivalTime.HasValue)
                {
                    arriveTime = AdjustTime(
                        ref previousTime,
                        record.ArrivalTime.Value,
                        record.ArrivalTime.Value - record.StartTime,
                        adjustTime
                    );
                }
                if (record.InspectionTime.HasValue)
                {
                    inspectTime = AdjustTime(
                        ref previousTime,
                        record.InspectionTime.Value,
                        record.InspectionTime.Value - record.ArrivalTime!.Value,
                        adjustTime
                    );
                }
                if (record.ReInspectionTime.HasValue)
                {
                    reinspectTime = AdjustTime(
                        ref previousTime,
                        record.ReInspectionTime.Value,
                        record.ReInspectionTime.Value - record.InspectionTime!.Value,
                        adjustTime
                    );
                }

                phase = Phase.ImportFromEMS(
                    lap.LengthInKm,
                    lap.MaxRecoveryTimeInMins,
                    lap.RestTimeInMins,
                    type,
                    lap.IsFinal,
                    null,
                    startTimestamp,
                    arriveTime,
                    inspectTime,
                    reinspectTime,
                    record.IsReinspectionRequired,
                    record.IsRequiredInspectionRequired,
                    record.IsRequiredInspectionRequired
                );

                finalRecord = record;
            }
            else
            {
                phase = new Phase(
                    lap.LengthInKm,
                    lap.MaxRecoveryTimeInMins,
                    lap.RestTimeInMins,
                    type,
                    lap.IsFinal,
                    null,
                    null
                );
            }

            phases.Add(phase);
        }
        var ruleset = CompetitionFactory.MapCompetitionRuleset(emsCompetition.Type);

        CompetitionType competitionType;
        if (emsCompetition.Name.NContains("първенство"))
        {
            competitionType = CompetitionType.Championship;
        }
        else if (emsCompetition.Name.NContains("*"))
        {
            competitionType = CompetitionType.Star;
        }
        else
        {
            competitionType = CompetitionType.Qualification;
        }

        var participation = new Participation(emsCompetition.Name, ruleset, competitionType, combination, phases);
        if (finalRecord?.Result?.Type == EmsResultType.FailedToQualify)
        {
            participation.FailToQualify([FtqCode.GA], null);
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

    static DateTime AdjustTime(ref DateTimeOffset? previousTime, DateTime currentTime, TimeSpan diff, bool shouldAdjust)
    {
        if (!shouldAdjust)
        {
            return currentTime;
        }
        currentTime = previousTime.HasValue ? (previousTime.Value + diff).DateTime : DateTimeOffset.Now.DateTime;
        previousTime = currentTime;
        return currentTime;
    }
}
