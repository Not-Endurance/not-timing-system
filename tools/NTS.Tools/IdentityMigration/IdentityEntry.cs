namespace NTS.Tools.IdentityMigration;

/// <summary>
/// One thing the owner decides (#636): a group that is held, or a pair of groups that are only a spelling variant, a near-miss or a
/// conflict apart. It is told by a key of normalised spellings and the FEI ID, and carries what the decisions file says of it.
/// </summary>
public sealed class IdentityEntry
{
    internal IdentityEntry(
        string key,
        ReviewClass match,
        IdentityGroupPlan left,
        IdentityGroupPlan? right,
        IReadOnlyList<SpellingLink> links
    )
    {
        Key = key;
        Class = match;
        Left = left;
        Right = right;
        Links = links;
    }

    public string Key { get; }
    public ReviewClass Class { get; }

    /// <summary>The held group, or the first group of the pair.</summary>
    public IdentityGroupPlan Left { get; }

    /// <summary>The second group of the pair; none for a held group.</summary>
    public IdentityGroupPlan? Right { get; }

    /// <summary>The evidence: the pairs of spellings that are alike or disagree.</summary>
    public IReadOnlyList<SpellingLink> Links { get; }

    /// <summary>What the decisions file says, by the item or by the rule of its class; none while it is to be decided.</summary>
    public IdentityDecision? Decision { get; internal set; }

    /// <summary>Whether the decision is the rule of the class and not the item's own.</summary>
    public bool ByRule { get; internal set; }

    /// <summary>
    /// Whether a decision of <c>same</c> takes effect. It does not while a group in the pair is held and has not been decided to be
    /// one: the owner decides the held group first.
    /// </summary>
    public bool IsApplied { get; internal set; }

    public bool IsDecided => Decision != null;
}
