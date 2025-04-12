using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities.LapRecords;
using NTS.Warp.ACL.Entities.Participants;
using NTS.Warp.ACL.Models;
using EmsParticipation = NTS.Warp.ACL.Entities.Participations.EmsParticipation;

namespace NTS.Warp.ACL.Factories;

public class ParticipationFactory
{
    public static EmsParticipation CreateEms(Participation participation)
    {
        var athlete = AthleteFactory.Create(participation);
        var horse = HorseFactory.Create(participation);

        var state = new EmsParticipantState
        {
            Number = participation.Combination.Number.ToString(),
            MaxAverageSpeedInKmPh = (int?)participation.Combination?.MinAverageSpeed?.ToDouble(),
            Unranked = true, // Cannot be fixed easy because IsNotRanked is on Ranking level. Not necessary in current Witness
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
}
