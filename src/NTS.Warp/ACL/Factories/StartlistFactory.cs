using NTS.Warp.ACL.Entities;

namespace NTS.Warp.ACL.Factories;

public static class StartlistFactory
{
    public static Dictionary<int, EmsStartlist> Create(IEnumerable<ParticipationModel> participations)
    {
        var emsParticipations = participations.Select(ParticipationFactory.CreateEms);
        var startlists = new Dictionary<int, EmsStartlist>();
        foreach (var emsParticipation in emsParticipations)
        {
            var toSkip = 0;
            var records = emsParticipation.Participant.LapRecords.Where(x =>
                x.Result == null || !x.Result.IsNotQualified
            );
            foreach (var record in records)
            {
                var entry = new EmsStartlistEntry(emsParticipation, toSkip);
                if (startlists.ContainsKey(entry.Stage))
                {
                    startlists[entry.Stage].Add(entry);
                }
                else
                {
                    startlists.Add(entry.Stage, new EmsStartlist([entry]));
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
                        startlists.Add(nextEntry.Stage, new EmsStartlist([nextEntry]));
                    }
                }
                toSkip++;
            }
        }
        return startlists;
    }
}
