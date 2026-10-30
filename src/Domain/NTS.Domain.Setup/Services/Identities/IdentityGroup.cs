namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// Copies that are one Athlete or one Horse because a chain of automatic matches joins them (#635, ADR-0010): each copy is in one
/// group, a copy that matches nothing in a group of its own. A group in which two copies disagree is held: it is one only if a
/// person says so.
/// </summary>
public sealed class IdentityGroup
{
    public IdentityGroup(
        IReadOnlyList<IdentityObservation> members,
        IReadOnlyList<IdentityLink> links,
        IReadOnlyList<IdentityLink> conflicts
    )
    {
        Members = members;
        Links = links;
        Conflicts = conflicts;
        Classes = [.. links.Select(x => x.Class).Distinct().Order()];
    }

    /// <summary>The copies, in the order they were given.</summary>
    public IReadOnlyList<IdentityObservation> Members { get; }

    /// <summary>Every pair of its copies that an automatic rule makes one, with the rule: what a report lists so that no merge is silent.</summary>
    public IReadOnlyList<IdentityLink> Links { get; }

    /// <summary>The pairs of its copies that disagree.</summary>
    public IReadOnlyList<IdentityLink> Conflicts { get; }

    /// <summary>The automatic rules that joined the group, each once, in the order of <see cref="MatchClass"/>.</summary>
    public IReadOnlyList<MatchClass> Classes { get; }

    /// <summary>Whether two of its copies disagree, so that the whole group is for a person to decide.</summary>
    public bool IsHeld => Conflicts.Count > 0;
}
