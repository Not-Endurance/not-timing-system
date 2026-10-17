namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// Two groups that a person has to say are one or two (#635, ADR-0010): some copy of the one is a spelling variant or a near-miss
/// of some copy of the other, or the two disagree. The class is the strongest of what the pairs between them say: a conflict,
/// then a spelling variant, then a near-miss.
/// </summary>
public sealed class ReviewItem
{
    public ReviewItem(IdentityGroup left, IdentityGroup right, MatchClass match, IReadOnlyList<IdentityLink> links)
    {
        Left = left;
        Right = right;
        Class = match;
        Links = links;
    }

    public IdentityGroup Left { get; }
    public IdentityGroup Right { get; }
    public MatchClass Class { get; }

    /// <summary>Every pair of a copy of the one and a copy of the other that is alike, with its class: the evidence.</summary>
    public IReadOnlyList<IdentityLink> Links { get; }
}
