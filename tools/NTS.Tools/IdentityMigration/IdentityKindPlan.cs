using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// What becomes of the Athletes, or of the Horses (#636): the copies read, the spellings they come to, the groups the automatic
/// rules make, what is left to the owner and what the owner has decided, and the identities that follow. Nothing here writes.
/// </summary>
public sealed class IdentityKindPlan
{
    internal IdentityKindPlan(
        IdentityKind kind,
        IReadOnlyList<IdentityCopy> read,
        IReadOnlyDictionary<string, string> events,
        IdentityDecisions decisions
    )
    {
        Kind = kind;
        Copies = [.. read.Where(x => x.Kind == kind)];
        Unreadable = [.. Copies.Where(IsUnreadable)];
        Spellings =
        [
            .. Copies
                .Where(x => !IsUnreadable(x))
                .GroupBy(IdentityKeys.SpellingOf, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new IdentitySpelling(x.Key, [.. x], events)),
        ];

        var spellings = Spellings.ToDictionary(x => x.Observation);
        var grouping = IdentityMatcher.Group(kind, [.. Spellings.Select(x => x.Observation)]);
        var groups = grouping.Groups.ToDictionary(x => x, x => new IdentityGroupPlan(kind, x, spellings));
        Groups = [.. grouping.Groups.Select(x => groups[x])];
        Entries = [.. EntriesOf(grouping, groups, spellings, decisions)];
        (Identities, Contradictions) = Resolve(Groups, Entries);
    }

    public IdentityKind Kind { get; }

    /// <summary>Every copy read, a Setup row among them.</summary>
    public IReadOnlyList<IdentityCopy> Copies { get; }

    /// <summary>The copies that have no name at all, so that nothing can be said of them; they are listed and left out of the rest.</summary>
    public IReadOnlyList<IdentityCopy> Unreadable { get; }

    /// <summary>The distinct spellings of the copies that have a name, in the order of their keys.</summary>
    public IReadOnlyList<IdentitySpelling> Spellings { get; }

    /// <summary>What the automatic rules make of the spellings, a spelling that matches nothing a group of its own.</summary>
    public IReadOnlyList<IdentityGroupPlan> Groups { get; }

    /// <summary>The groups that are held and the pairs of groups to review, the most blocking class first, decided or not.</summary>
    public IReadOnlyList<IdentityEntry> Entries { get; }

    /// <summary>The Athletes or the Horses the registry will have, after the automatic rules and the decisions.</summary>
    public IReadOnlyList<PlannedIdentity> Identities { get; }

    /// <summary>Decisions of <c>different</c> for a pair that other decisions of <c>same</c> have joined all the same.</summary>
    public IReadOnlyList<IdentityEntry> Contradictions { get; }

    /// <summary>How many different legacy ids the copies have, 0 being none.</summary>
    public int DistinctLegacyIds => Copies.Select(x => x.LegacyId).Where(x => x != null).Distinct().Count();

    /// <summary>The rows of Setup that exist.</summary>
    public int SetupRows => Spellings.Count(x => x.IsSetupRow);

    /// <summary>The identities that no row of Setup is: a row is made for each from its latest copy.</summary>
    public int RowsToCreate => Identities.Count(x => x.SetupRows == 0);

    /// <summary>The rows of Setup that are another row's duplicate and would merge into it.</summary>
    public int DuplicateRows => Identities.Where(x => x.SetupRows > 1).Sum(x => x.SetupRows - 1);

    /// <summary>The copies of an identity that has no row of Setup any more.</summary>
    public int CopiesWithoutRow => Identities.Where(x => x.SetupRows == 0).Sum(x => x.Copies);

    /// <summary>The entries with no decision yet.</summary>
    public int ToReview => Entries.Count(x => !x.IsDecided);

    public int Decided => Entries.Count(x => x.IsDecided);

    /// <summary>The decisions of <c>same</c> that wait, because a group in the pair is held and the owner has not said it is one.</summary>
    public int Waiting => Entries.Count(x => x.Decision == IdentityDecision.Same && !x.IsApplied);

    static bool IsUnreadable(IdentityCopy copy)
    {
        return copy.Name == null && copy.NameEnglish == null;
    }

    static ReviewClass ClassOf(MatchClass match)
    {
        return match switch
        {
            MatchClass.Conflict => ReviewClass.Conflict,
            MatchClass.SpellingVariant => ReviewClass.SpellingVariant,
            MatchClass.NearMiss => ReviewClass.NearMiss,
            _ => throw new ArgumentOutOfRangeException(nameof(match), match, "An automatic class is not for review."),
        };
    }

    static List<IdentityEntry> EntriesOf(
        IdentityGrouping grouping,
        IReadOnlyDictionary<IdentityGroup, IdentityGroupPlan> groups,
        IReadOnlyDictionary<IdentityObservation, IdentitySpelling> spellings,
        IdentityDecisions decisions
    )
    {
        var entries = groups
            .Values.Where(x => x.IsHeld)
            .Select(x => new IdentityEntry(x.Key, ReviewClass.Held, x, null, x.Conflicts))
            .Concat(
                grouping.Review.Select(x => new IdentityEntry(
                    IdentityKeys.PairOf(groups[x.Left].Key, groups[x.Right].Key),
                    ClassOf(x.Class),
                    groups[x.Left],
                    groups[x.Right],
                    [.. x.Links.Select(y => IdentityGroupPlan.Link(y, spellings))]
                ))
            )
            .OrderBy(x => x.Class)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var entry in entries)
        {
            var own = decisions.Item(entry.Key);
            entry.Decision = own ?? decisions.Rule(entry.Class);
            entry.ByRule = own == null && entry.Decision != null;
        }

        return entries;
    }

    /// <summary>
    /// The spellings that are one, after the decisions. A group is one; a group that is held and has not been decided to be one is
    /// not, and each of its spellings stands alone. A decision of <c>same</c> joins the groups of its pair, unless one of them is
    /// held and undecided: then it waits for that. A decision of <c>different</c> that the others have overruled is a contradiction.
    /// </summary>
    static (List<PlannedIdentity> Identities, List<IdentityEntry> Contradictions) Resolve(
        IReadOnlyList<IdentityGroupPlan> groups,
        IReadOnlyList<IdentityEntry> entries
    )
    {
        var split = entries
            .Where(x => x.Class == ReviewClass.Held && x.Decision != IdentityDecision.Same)
            .Select(x => x.Left)
            .ToHashSet();
        var units = new List<IReadOnlyList<IdentitySpelling>>();
        var unitOf = new Dictionary<IdentitySpelling, int>();
        foreach (var group in groups)
        {
            foreach (var members in UnitsOf(group, split.Contains(group)))
            {
                units.Add(members);
                foreach (var spelling in members)
                {
                    unitOf[spelling] = units.Count - 1;
                }
            }
        }

        var parent = Enumerable.Range(0, units.Count).ToArray();
        var joined = new List<int>();
        foreach (var entry in entries)
        {
            entry.IsApplied = entry.Class == ReviewClass.Held && entry.Decision == IdentityDecision.Same;
            if (entry.Class == ReviewClass.Held || entry.Decision != IdentityDecision.Same)
            {
                continue;
            }

            if (split.Contains(entry.Left) || split.Contains(entry.Right!))
            {
                continue;
            }

            var left = unitOf[entry.Left.Spellings[0]];
            parent[Root(parent, left)] = Root(parent, unitOf[entry.Right!.Spellings[0]]);
            joined.Add(left);
            entry.IsApplied = true;
        }

        var byDecision = joined.Select(x => Root(parent, x)).ToHashSet();
        var identities = Enumerable
            .Range(0, units.Count)
            .GroupBy(x => Root(parent, x))
            .Select(x => new PlannedIdentity(
                [.. x.SelectMany(y => units[y]).OrderBy(y => y.Key, StringComparer.Ordinal)],
                byDecision.Contains(x.Key)
            ))
            .OrderBy(x => x.Spellings[0].Key, StringComparer.Ordinal)
            .ToList();
        var contradictions = entries
            .Where(x =>
                x is { Decision: IdentityDecision.Different, ByRule: false, Right: not null }
                && Root(parent, unitOf[x.Left.Spellings[0]]) == Root(parent, unitOf[x.Right.Spellings[0]])
            )
            .ToList();
        return (identities, contradictions);
    }

    /// <summary>The spellings that go together: a group, or each spelling of a group on its own when it is held.</summary>
    static IEnumerable<IReadOnlyList<IdentitySpelling>> UnitsOf(IdentityGroupPlan group, bool isSplit)
    {
        return isSplit ? group.Spellings.Select(x => (IReadOnlyList<IdentitySpelling>)[x]) : [group.Spellings];
    }

    static int Root(int[] parent, int unit)
    {
        while (parent[unit] != unit)
        {
            parent[unit] = parent[parent[unit]];
            unit = parent[unit];
        }

        return unit;
    }
}
