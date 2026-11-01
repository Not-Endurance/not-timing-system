namespace NTS.Domain.Setup.Services.Identities;

/// <summary>What became of a set of copies of Athletes or Horses (#635, ADR-0010): the groups that are one, and the pairs of groups to review.</summary>
public sealed class IdentityGrouping
{
    public IdentityGrouping(IReadOnlyList<IdentityGroup> groups, IReadOnlyList<ReviewItem> review)
    {
        Groups = groups;
        Review = review;
    }

    /// <summary>Every copy is in one group; they are in the order the first copy of each came in.</summary>
    public IReadOnlyList<IdentityGroup> Groups { get; }

    /// <summary>The pairs of groups that are only a spelling variant, a near-miss or a conflict apart, in the order of their groups.</summary>
    public IReadOnlyList<ReviewItem> Review { get; }
}
