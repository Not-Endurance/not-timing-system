using NC.Domain.Exceptions;

namespace NoTiming.Domain.Core.Aggregates.Results;

// Places every entry of a ranking so the placings can be stored and frozen. Entries still on course keep the
// position they have now. A ranking of one entry places that entry first.
public static class RankingFinalization
{
    public static IReadOnlyDictionary<int, int> PlaceEntries(Ranking ranking)
    {
        var result = new Result(ranking);
        var placings = new Dictionary<int, int>();
        foreach (var entry in result.Entries)
        {
            var rank = entry.Rank ?? (result.Entries.Count == 1 ? 1 : null);
            if (rank == null)
            {
                throw new DomainException($"No placing could be found for participation {entry.ParticipationId}.");
            }
            placings[entry.ParticipationId] = rank.Value;
        }
        return placings;
    }
}
