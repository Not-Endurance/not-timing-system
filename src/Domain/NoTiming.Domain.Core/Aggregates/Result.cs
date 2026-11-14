using NoTiming.Domain.Core.Aggregates.Results;
using NoTiming.Domain.Core.Objects.Rankers;

namespace NoTiming.Domain.Core.Aggregates;

public class Result : Aggregate, IEventScoped
{
    static readonly FeiRanker FEI_RANKER = new();

    protected Result(
        int? id,
        int? rankingId,
        string? name,
        bool englishNames,
        ParticipationCategory? category,
        IEnumerable<ParticipationResult> entries,
        int eventId,
        int? storedDomainVersion = null
    )
        : base(id, storedDomainVersion: storedDomainVersion)
    {
        EventId = eventId;
        RankingId = rankingId;
        Name = Required(nameof(Name), name);
        EnglishNames = englishNames;
        Category = Required(nameof(Category), category);

        var participationResults = Required(nameof(Entries), entries).ToList();
        AreUnique(nameof(Participations), participationResults.Select(x => x.Participation)).ToList();
        Entries = RankIfRequired(participationResults).AsReadOnly();
    }

    public Result(Ranking ranking)
        : this(
            ranking.Id,
            ranking.Id,
            ranking.Name,
            ranking.EnglishNames,
            ranking.Category,
            ranking.Entries.Select(ParticipationResult.From),
            ranking.EventId
        ) { }

    public int EventId { get; }
    public int? RankingId { get; }
    public string Name { get; }
    public bool EnglishNames { get; }
    public ParticipationCategory Category { get; }
    public IReadOnlyList<ParticipationResult> Entries { get; }
    public bool IsRanked => Entries.Count > 1;
    public string Title => $"{Category}: {Name}";

    public override string ToString()
    {
        return $"{Name} {Category}: {Entries.Count}";
    }

    static List<ParticipationResult> RankIfRequired(List<ParticipationResult> entries)
    {
        if (entries.Count <= 1)
        {
            return entries;
        }
        return IsFinal(entries) ? entries.OrderBy(x => x.Rank).ToList() : Rank(entries);
    }

    static bool IsFinal(List<ParticipationResult> entries)
    {
        return entries.All(x => x.Rank != null);
    }

    static List<ParticipationResult> Rank(IReadOnlyCollection<ParticipationResult> entries)
    {
        var ranked = FEI_RANKER.Rank(entries);
        var rank = 0;
        foreach (var entry in ranked)
        {
            entry.Rank = ++rank;
        }
        return ranked;
    }
}
