using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>Two spellings and the rule that decided how they relate (#636).</summary>
public sealed class SpellingLink
{
    public SpellingLink(IdentitySpelling one, IdentitySpelling another, MatchClass match)
    {
        One = one;
        Another = another;
        Class = match;
    }

    public IdentitySpelling One { get; }
    public IdentitySpelling Another { get; }
    public MatchClass Class { get; }
}

/// <summary>
/// Spellings that an automatic rule makes one Athlete or one Horse, through any chain (#636, ADR-0010): a group of its own for a
/// spelling that matches nothing. Every link that joined it is kept, so that the report can list each merge by the rule that made it.
/// </summary>
public sealed class IdentityGroupPlan
{
    internal IdentityGroupPlan(
        IdentityKind kind,
        IdentityGroup group,
        IReadOnlyDictionary<IdentityObservation, IdentitySpelling> spellings
    )
    {
        Spellings = [.. group.Members.Select(x => spellings[x])];
        Links = [.. group.Links.Select(x => Link(x, spellings))];
        Conflicts = [.. group.Conflicts.Select(x => Link(x, spellings))];
        Key = IdentityKeys.GroupOf(kind, Spellings);
    }

    /// <summary>The key a decision about the group is written under: <c>athlete (fei 100): ivan petrov | иван петров</c>.</summary>
    public string Key { get; }

    public IReadOnlyList<IdentitySpelling> Spellings { get; }

    /// <summary>Every pair of its spellings that an automatic rule makes one, with the rule.</summary>
    public IReadOnlyList<SpellingLink> Links { get; }

    /// <summary>The pairs of its spellings that disagree.</summary>
    public IReadOnlyList<SpellingLink> Conflicts { get; }

    /// <summary>Two of its spellings disagree, so that it is one only if the owner says so.</summary>
    public bool IsHeld => Conflicts.Count > 0;

    internal static SpellingLink Link(
        IdentityLink link,
        IReadOnlyDictionary<IdentityObservation, IdentitySpelling> spellings
    )
    {
        return new SpellingLink(spellings[link.One], spellings[link.Another], link.Class);
    }
}
