using NTS.Application.Core;
using NTS.Warp.ACL.Entities.LapRecords;
using NTS.Warp.ACL.Entities.Participants;
using NTS.Warp.ACL.Entities.Participations;
using NTS.Warp.ACL.Models;

namespace NTS.Warp.ACL.Factories;

public class ParticipationFactory
{
    public static EmsParticipation CreateEms(ParticipationModel participation)
    {
        var athlete = AthleteFactory.Create(participation.Combination.Athlete);
        var horse = HorseFactory.Create(participation.Combination.Horse);

        var state = new EmsParticipantState
        {
            Number = participation.Combination.Number.ToString(),
            MaxAverageSpeedInKmPh = (int?)participation.Combination.MinAverageSpeed,
            Unranked = true, // Cannot be fixed easy because IsNotRanked is on Ranking level. Not necessary in current Witness
        };
        var emsParticipant = new EmsParticipant(athlete, horse, state);
        var emsLaps = LapFactory.Create(participation.Phases).ToList();
        if (participation.Phases != null && participation.Phases.Length > 0)
        {
            for (var i = 0; i < participation.Phases.Length; i++)
            {
                var phase = participation.Phases[i];
                var emsLap = emsLaps[i];
                if (phase.StartTime == null)
                {
                    break;
                }
                var emsRecord = new EmsLapRecord(phase.StartTime.Value, emsLap)
                {
                    StartTime = phase.StartTime.Value,
                    ArrivalTime = phase.ArriveTime,
                    InspectionTime = phase.PresentTime,
                    ReInspectionTime = phase.RepresentTime,
                };
                emsParticipant.Add(emsRecord);
            }
        }

        var competition = CompetitionFactory.Create(participation);

        return new EmsParticipation(emsParticipant, competition);
    }
}
