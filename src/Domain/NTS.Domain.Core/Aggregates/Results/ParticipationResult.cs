namespace NTS.Domain.Core.Aggregates.Results;

public class ParticipationResult : Entity
{
    public static ParticipationResult From(RankingEntry entry)
    {
        return new ParticipationResult(entry.Participation, entry.IsNotRanked, entry.Rank, entry.Id);
    }

    public ParticipationResult(
        Participation? participation,
        bool isNotRanked = false,
        int? rank = null,
        int? id = null
    )
        : base(id ?? participation?.Id)
    {
        Participation = Required(nameof(Participation), participation);
        Rank = rank;
        IsNotRanked = isNotRanked;
    }

    public Participation Participation { get; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }
    public int ParticipationId => Participation.Id;

    public override string ToString()
    {
        return IsNotRanked ? $"{X_string} {Participation}" : Participation.ToString();
    }
}
