namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// Whether two copies of an Athlete or a Horse are the same one, and the rule that decided it (#635, ADR-0010). Pure: it reads
/// what the copies say of themselves and nothing else, so that Setup's guard, the migration of the registry and the search all
/// decide alike. A match on a FEI ID, an exact name, a name under the official transliteration or a legacy id is automatic;
/// a spelling variant, a near-miss and a conflict are for a review; the country and the club decide nothing.
/// </summary>
public static class IdentityMatcher
{
    /// <summary>Names this many edits apart or fewer are a near-miss, if they are also fewer than half the length of the shorter.</summary>
    const int MAX_EDITS = 2;

    /// <summary>
    /// How two copies relate, by the rule that decided it, or none when they do not. The FEI IDs and the legacy ids are asked
    /// before the names, but an id that the names do not bear out is a conflict, and so are equal names under FEI IDs
    /// that differ. Which of the two copies is asked about first does not matter.
    /// </summary>
    public static MatchClass? Classify(IdentityKind kind, IdentityObservation one, IdentityObservation another)
    {
        return Classify(IdentityProfile.Of(kind, one), IdentityProfile.Of(kind, another));
    }

    /// <summary>
    /// The copies among those given that the one is alike, each with the rule that decided it, in the order they were given: what
    /// Setup asks when an Athlete or a Horse is about to be registered or renamed. The copy itself, if it is among them, is not.
    /// </summary>
    public static IReadOnlyList<IdentityLink> Among(
        IdentityKind kind,
        IdentityObservation observation,
        IEnumerable<IdentityObservation> candidates
    )
    {
        var profile = IdentityProfile.Of(kind, observation);
        var found = new List<IdentityLink>();
        foreach (var candidate in candidates.Where(x => !ReferenceEquals(x, observation)))
        {
            if (Classify(profile, IdentityProfile.Of(kind, candidate)) is { } match)
            {
                found.Add(new IdentityLink(observation, candidate, match));
            }
        }

        return found;
    }

    /// <summary>
    /// Sorts the copies into the identities they are: copies that an automatic rule makes one, through any chain, are a group,
    /// a group that holds two copies that disagree is held, and what a spelling variant, a near-miss or a conflict relates
    /// across groups is a review item. The same copies, in the same order, give the same groups.
    /// </summary>
    public static IdentityGrouping Group(IdentityKind kind, IReadOnlyList<IdentityObservation> observations)
    {
        var profiles = observations.Select(x => IdentityProfile.Of(kind, x)).ToList();
        var verdicts = new List<(int One, int Another, MatchClass Class)>();
        var joins = new Joins(profiles.Count);
        for (var one = 0; one < profiles.Count; one++)
        {
            for (var another = one + 1; another < profiles.Count; another++)
            {
                if (Classify(profiles[one], profiles[another]) is not { } verdict)
                {
                    continue;
                }

                verdicts.Add((one, another, verdict));
                if (verdict.IsAutomatic())
                {
                    joins.Join(one, another);
                }
            }
        }

        var memberships = Enumerable.Range(0, profiles.Count).GroupBy(joins.RootOf).OrderBy(x => x.Min()).ToList();
        var groupOf = new int[profiles.Count];
        for (var group = 0; group < memberships.Count; group++)
        {
            foreach (var member in memberships[group])
            {
                groupOf[member] = group;
            }
        }

        var groups = memberships
            .Select(
                (members, group) =>
                    new IdentityGroup(
                        [.. members.Select(x => observations[x])],
                        [
                            .. LinksOf(
                                verdicts.Where(x => x.Class.IsAutomatic() && groupOf[x.One] == group),
                                observations
                            ),
                        ],
                        [
                            .. LinksOf(
                                verdicts.Where(x =>
                                    x.Class == MatchClass.Conflict
                                    && groupOf[x.One] == group
                                    && groupOf[x.Another] == group
                                ),
                                observations
                            ),
                        ]
                    )
            )
            .ToList();
        var review = verdicts
            .Where(x => !x.Class.IsAutomatic() && groupOf[x.One] != groupOf[x.Another])
            .GroupBy(x =>
                (
                    Left: Math.Min(groupOf[x.One], groupOf[x.Another]),
                    Right: Math.Max(groupOf[x.One], groupOf[x.Another])
                )
            )
            .OrderBy(x => x.Key.Left)
            .ThenBy(x => x.Key.Right)
            .Select(x => new ReviewItem(
                groups[x.Key.Left],
                groups[x.Key.Right],
                x.Select(y => y.Class).OrderBy(Strength).First(),
                [.. LinksOf(x, observations)]
            ))
            .ToList();
        return new IdentityGrouping(groups, review);
    }

    internal static MatchClass? Classify(IdentityProfile one, IdentityProfile another)
    {
        var names = Closest(one, another);
        var sameFei = one.FeiId != null && one.FeiId == another.FeiId;
        var differentFei = one.FeiId != null && another.FeiId != null && one.FeiId != another.FeiId;
        var sameLegacyId = one.LegacyId != null && one.LegacyId == another.LegacyId;
        var sameName = names is NameRelation.Exact or NameRelation.Strict or NameRelation.Looser;

        if (differentFei)
        {
            return sameName || sameLegacyId ? MatchClass.Conflict : null; // two FEI IDs are two people unless the names say they are not
        }

        if (sameFei)
        {
            return names == NameRelation.None ? MatchClass.Conflict : MatchClass.SameFeiId;
        }

        return names switch
        {
            NameRelation.Exact => MatchClass.ExactName,
            NameRelation.Strict => MatchClass.StrictTransliteration,
            _ when sameLegacyId => names == NameRelation.None ? MatchClass.Conflict : MatchClass.SameLegacyId,
            NameRelation.Looser => MatchClass.SpellingVariant,
            NameRelation.NearMiss => MatchClass.NearMiss,
            _ => null,
        };
    }

    static IEnumerable<IdentityLink> LinksOf(
        IEnumerable<(int One, int Another, MatchClass Class)> verdicts,
        IReadOnlyList<IdentityObservation> observations
    )
    {
        return verdicts.Select(x => new IdentityLink(observations[x.One], observations[x.Another], x.Class));
    }

    /// <summary>What a review item is told by when the pairs between its groups say different things: a conflict first.</summary>
    static int Strength(MatchClass match)
    {
        return match switch
        {
            MatchClass.Conflict => 0,
            MatchClass.SpellingVariant => 1,
            _ => 2,
        };
    }

    /// <summary>The closest the names of two copies come, across the name and the name in English of both.</summary>
    static NameRelation Closest(IdentityProfile one, IdentityProfile another)
    {
        var closest = NameRelation.None;
        foreach (var mine in one.Names)
        {
            foreach (var theirs in another.Names)
            {
                var relation = Between(mine, theirs);
                if (relation < closest)
                {
                    closest = relation;
                }
            }
        }

        return closest;
    }

    static NameRelation Between(IdentityName one, IdentityName another)
    {
        if (one.Normalised == another.Normalised)
        {
            return NameRelation.Exact;
        }

        if (one.Strict == another.Strict)
        {
            return NameRelation.Strict;
        }

        if (one.Looser == another.Looser)
        {
            return NameRelation.Looser;
        }

        return IsNearMiss(one.Sorted, another.Sorted) ? NameRelation.NearMiss : NameRelation.None;
    }

    static bool IsNearMiss(string one, string another)
    {
        var shorter = Math.Min(one.Length, another.Length);
        if (shorter == 0 || Math.Abs(one.Length - another.Length) > MAX_EDITS)
        {
            return false;
        }

        var edits = Edits(one, another);
        return edits <= MAX_EDITS && edits * 2 < shorter;
    }

    /// <summary>The number of single-letter changes that make one text the other (Levenshtein).</summary>
    static int Edits(string one, string another)
    {
        var before = new int[another.Length + 1];
        var row = new int[another.Length + 1];
        for (var column = 0; column <= another.Length; column++)
        {
            before[column] = column;
        }

        for (var line = 1; line <= one.Length; line++)
        {
            row[0] = line;
            for (var column = 1; column <= another.Length; column++)
            {
                var cost = one[line - 1] == another[column - 1] ? 0 : 1;
                row[column] = Math.Min(Math.Min(row[column - 1] + 1, before[column] + 1), before[column - 1] + cost);
            }

            (before, row) = (row, before);
        }

        return before[another.Length];
    }
}
