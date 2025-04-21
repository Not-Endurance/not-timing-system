using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities.LapRecords;
using NTS.Warp.ACL.Entities.Participants;
using NTS.Warp.ACL.Entities.Participations;
using NTS.Warp.ACL.Models;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class ParticipationFactory
{
    public static EmsParticipation CreateEms(ParticipationWarpDto participation)
    {
        var athlete = AthleteFactory.Create(participation.Athlete);
        var horse = HorseFactory.Create(participation.Horse);

        var state = new EmsParticipantState
        {
            Number = participation.Number.ToString(),
            MaxAverageSpeedInKmPh = (int?)participation.MinAverageSpeed,
            Unranked = true, // Cannot be fixed easy because IsNotRanked is on Ranking level. Not necessary in current Witness
        };
        var emsParticipant = new EmsParticipant(athlete, horse, state);
        var emsLaps = LapFactory.Create(participation.Phases).ToList();
        for (var i = 0; i < participation.Phases.Length; i++)
        {
            var phase = participation.Phases[i];
            var emsLap = emsLaps[i];
            if (phase.StartTime == null)
            {
                break;
            }
            var emsRecord = new EmsLapRecord(phase.StartTime.Value.Date, emsLap)
            {
                ArrivalTime = phase.ArriveTime?.DateTime,
                InspectionTime = phase.PresentTime?.DateTime,
                ReInspectionTime = phase.RepresentTime?.DateTime,
            };
            emsParticipant.Add(emsRecord);
        }
        var competition = CompetitionFactory.Create(participation);

        return new EmsParticipation(emsParticipant, competition);
    }
}
