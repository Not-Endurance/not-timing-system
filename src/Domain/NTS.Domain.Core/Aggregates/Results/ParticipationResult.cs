namespace NTS.Domain.Core.Aggregates.Results;

/// <summary>A Participation as one line of the Results, with the mark and the rank the Results give it.</summary>
public class ParticipationResult : Entity
{
    public ParticipationResult(Participation? participation, bool isNotRanked = false, int? rank = null)
        : base(participation?.Id)
    {
        Participation = Required(nameof(Participation), participation);
        Rank = rank;
        IsNotRanked = isNotRanked;
    }

    public Participation Participation { get; }
    public int? Rank { get; internal set; }
    public bool IsNotRanked { get; }
    public Guid ParticipationId => Participation.Id;

    public override string ToString()
    {
        return IsNotRanked ? $"{X_string} {Participation}" : Participation.ToString();
    }
}
