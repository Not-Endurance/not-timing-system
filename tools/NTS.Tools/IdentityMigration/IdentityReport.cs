using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The Markdown report of the dry run (#636): the counts, every merge the automatic rules make by the rule that makes it, the Setup
/// duplicates and the rows to create, and what is left for the owner to decide with the evidence for each item. It is made of the plan
/// alone, with no date and no machine in it, so that the same data and decisions give the same text. It holds real names and is
/// written outside git.
/// </summary>
public static class IdentityReport
{
    static readonly MatchClass[] Automatic =
    [
        MatchClass.SameFeiId,
        MatchClass.ExactName,
        MatchClass.StrictTransliteration,
        MatchClass.SameLegacyId,
    ];
    static readonly (string Label, string[] Collections)[] Places =
    [
        ("Setup", [IdentityCopies.ATHLETES, IdentityCopies.HORSES]),
        ("configure events", [IdentityCopies.CONFIGURE_EVENTS]),
        ("Participations", [IdentityCopies.PARTICIPATIONS]),
        ("Rankings", [IdentityCopies.RANKINGS]),
        ("Handouts", [IdentityCopies.HANDOUTS]),
    ];

    public static string Render(IdentityPlan plan, string database, PlannedCutover? cutover = null)
    {
        var text = new StringWriter { NewLine = "\n" };
        text.WriteLine("# Identity migration: dry run");
        text.WriteLine();
        text.WriteLine($"Database: {database}");
        text.WriteLine();
        text.WriteLine(
            "This is a dry run. Nothing was written to the database. The report and the decisions file hold real names: keep them out of git."
        );
        text.WriteLine();
        WriteCounts(text, plan);
        WriteAutomatic(text, plan);
        WriteDuplicates(text, plan);
        WriteCreated(text, plan);
        WriteReview(text, plan);
        WriteMerged(text, plan);
        WriteDecisions(text, plan);
        WriteUnnamed(text, plan);
        if (cutover != null)
        {
            WriteCutover(text, cutover);
        }

        return text.ToString();
    }

    static void WriteCounts(TextWriter text, IdentityPlan plan)
    {
        text.WriteLine("## Counts");
        text.WriteLine();
        text.WriteLine("| | Athletes | Horses |");
        text.WriteLine("| --- | ---: | ---: |");
        Row(text, plan, "Copies read", x => x.Copies.Count);
        Row(text, plan, "Copies in Setup rows", x => InPlace(x, IdentityCopies.ATHLETES, IdentityCopies.HORSES));
        Row(text, plan, "Copies in configure events", x => InPlace(x, IdentityCopies.CONFIGURE_EVENTS));
        Row(text, plan, "Copies in Participations", x => InPlace(x, IdentityCopies.PARTICIPATIONS));
        Row(text, plan, "Copies in Rankings", x => InPlace(x, IdentityCopies.RANKINGS));
        Row(text, plan, "Copies in Handouts", x => InPlace(x, IdentityCopies.HANDOUTS));
        Row(text, plan, "Copies with no name (set aside)", x => x.Unreadable.Count);
        Row(text, plan, "Distinct spellings", x => x.Spellings.Count);
        Row(text, plan, "Distinct legacy ids (0 is none)", x => x.DistinctLegacyIds);
        Row(text, plan, "Identities found", x => x.Identities.Count);
        Row(text, plan, "Setup rows that exist", x => x.SetupRows);
        Row(text, plan, "Rows that would be created", x => x.RowsToCreate);
        Row(text, plan, "Setup duplicates that would merge", x => x.DuplicateRows);
        Row(text, plan, "Copies whose Setup row is gone", x => x.CopiesWithoutRow);
        Row(text, plan, "Left to review", x => x.ToReview);
        Row(text, plan, "Decided", x => x.Decided);
        text.WriteLine();
        if (plan.Kinds.Sum(x => x.ToReview) == 0)
        {
            text.WriteLine("Nothing is left to review.");
            text.WriteLine();
        }
    }

    static void WriteAutomatic(TextWriter text, IdentityPlan plan)
    {
        text.WriteLine("## Merges by the automatic rules");
        text.WriteLine();
        text.WriteLine(
            "Each line is a pair of spellings that a rule makes one. Spellings are joined through any chain of such pairs, so a group of several is one Athlete or one Horse, unless the group is held: two of its spellings disagree, and nothing in it is merged until it is decided."
        );
        text.WriteLine();
        foreach (var match in Automatic)
        {
            var lines = plan
                .Kinds.SelectMany(kind =>
                    kind.Groups.OrderBy(x => x.Key, StringComparer.Ordinal)
                        .SelectMany(group =>
                            group
                                .Links.Where(x => x.Class == match)
                                .Select(x => (Kind: kind.Kind, Link: Ordered(x), group.IsHeld))
                        )
                        .OrderBy(x => x.Link.One.Key, StringComparer.Ordinal)
                        .ThenBy(x => x.Link.Another.Key, StringComparer.Ordinal)
                )
                .ToList();
            text.WriteLine($"### {Title(match)} ({lines.Count})");
            text.WriteLine();
            text.WriteLine(Rule(match));
            text.WriteLine();
            foreach (var (kind, link, isHeld) in lines)
            {
                var held = isHeld ? " (in a held group: not merged until it is decided)" : "";
                text.WriteLine($"- {Word(kind)}: {Short(link.One)} = {Short(link.Another)}{held}");
            }

            EndList(text, lines.Count);
        }
    }

    static void WriteDuplicates(TextWriter text, IdentityPlan plan)
    {
        var duplicates = plan
            .Kinds.SelectMany(kind =>
                kind.Identities.Where(x => x.SetupRows > 1).Select(x => (Kind: kind.Kind, Identity: x))
            )
            .ToList();
        text.WriteLine($"## Setup duplicates that would merge ({plan.Kinds.Sum(x => x.DuplicateRows)})");
        text.WriteLine();
        text.WriteLine("Rows of Setup that are the same Athlete or Horse: they would become one row.");
        text.WriteLine();
        foreach (var (kind, identity) in duplicates)
        {
            var rows = identity.Spellings.Where(x => x.IsSetupRow).Select(Short);
            text.WriteLine($"- {Word(kind)}: {string.Join(" + ", rows)}");
        }

        EndList(text, duplicates.Count);
    }

    static void WriteCreated(TextWriter text, IdentityPlan plan)
    {
        var created = plan
            .Kinds.SelectMany(kind =>
                kind.Identities.Where(x => x.SetupRows == 0).Select(x => (Kind: kind.Kind, Identity: x))
            )
            .ToList();
        text.WriteLine($"## Rows that would be created ({created.Count})");
        text.WriteLine();
        text.WriteLine(
            "Athletes and Horses that only copies have, because their row of Setup is gone or never was: a row is made for each."
        );
        text.WriteLine();
        foreach (var (kind, identity) in created)
        {
            text.WriteLine($"- {Word(kind)}: {string.Join(" = ", identity.Spellings.Select(Short))}");
        }

        EndList(text, created.Count);
    }

    static void WriteReview(TextWriter text, IdentityPlan plan)
    {
        var open = plan.Kinds.SelectMany(x => x.Entries).Where(x => !x.IsDecided).ToList();
        if (open.Count == 0)
        {
            return;
        }

        text.WriteLine($"## To review ({open.Count})");
        text.WriteLine();
        text.WriteLine(
            "Set each item to `same` or `different` in the decisions file, under the key shown, or set a rule for a whole class, and run the dry run again: an item that is decided is no longer listed here."
        );
        text.WriteLine();
        foreach (var match in Enum.GetValues<ReviewClass>())
        {
            var items = open.Where(x => x.Class == match).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            text.WriteLine($"### {Title(match)} ({items.Count})");
            text.WriteLine();
            foreach (var item in items)
            {
                WriteEntry(text, item);
            }
        }
    }

    static void WriteEntry(TextWriter text, IdentityEntry entry)
    {
        text.WriteLine($"#### `{entry.Key}`");
        text.WriteLine();
        text.WriteLine(Intro(entry.Class));
        text.WriteLine();
        if (entry.Right == null)
        {
            WriteGroup(text, entry.Left);
            text.WriteLine("They disagree:");
            text.WriteLine();
            foreach (var link in entry.Links.Select(Ordered))
            {
                text.WriteLine($"- {Short(link.One)} and {Short(link.Another)}: {Words(link.Class)}");
            }

            text.WriteLine();
            return;
        }

        WriteGroup(text, entry.Left);
        WriteGroup(text, entry.Right);
        text.WriteLine("Between them:");
        text.WriteLine();
        foreach (var link in entry.Links.Select(Ordered))
        {
            text.WriteLine($"- {Short(link.One)} and {Short(link.Another)}: {Words(link.Class)}");
        }

        text.WriteLine();
        var shared = entry
            .Left.Spellings.SelectMany(x => x.Events)
            .Intersect(entry.Right.Spellings.SelectMany(x => x.Events))
            .Order(StringComparer.Ordinal)
            .ToList();
        text.WriteLine(
            shared.Count == 0 ? "Never in the same Event." : $"Seen in the same Event: {string.Join("; ", shared)}"
        );
        text.WriteLine();
    }

    static void WriteGroup(TextWriter text, IdentityGroupPlan group)
    {
        text.WriteLine($"Group `{group.Key}`:");
        text.WriteLine();
        foreach (
            var spelling in group
                .Spellings.OrderByDescending(x => x.IsSetupRow)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
        )
        {
            text.WriteLine($"- {Short(spelling)}");
            if (spelling.Countries.Count > 0)
            {
                text.WriteLine($"  - Country: {string.Join(", ", spelling.Countries)}");
            }

            if (spelling.Clubs.Count > 0)
            {
                text.WriteLine($"  - Club: {string.Join(", ", spelling.Clubs)}");
            }

            text.WriteLine($"  - Copies: {Copies(spelling)}");
            if (spelling.Events.Count > 0)
            {
                text.WriteLine($"  - Events: {string.Join("; ", spelling.Events)}");
            }

            if (spelling.Partners.Count > 0)
            {
                text.WriteLine($"  - Entered with: {string.Join(", ", spelling.Partners)}");
            }
        }

        text.WriteLine();
    }

    static void WriteMerged(TextWriter text, IdentityPlan plan)
    {
        var merged = plan
            .Kinds.SelectMany(x => x.Entries)
            .Where(x => x.IsApplied)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        text.WriteLine($"## Merges by your decisions ({merged.Count})");
        text.WriteLine();
        foreach (var entry in merged)
        {
            var how = entry.ByRule ? $"the rule for {Title(entry.Class).ToLowerInvariant()}" : "decided";
            text.WriteLine($"- `{entry.Key}` ({how}: same)");
        }

        EndList(text, merged.Count);
    }

    static void WriteDecisions(TextWriter text, IdentityPlan plan)
    {
        var entries = plan.Kinds.SelectMany(x => x.Entries).ToList();
        var contradictions = plan
            .Kinds.SelectMany(x => x.Contradictions)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        var waiting = entries
            .Where(x => x.Decision == IdentityDecision.Same && !x.IsApplied)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        text.WriteLine("## Decisions");
        text.WriteLine();
        text.WriteLine(
            $"- Decided by item: {entries.Count(x => x.IsDecided && !x.ByRule)}, by rule: {entries.Count(x => x.ByRule)}"
        );
        text.WriteLine($"- Matching nothing in this data: {plan.UnusedDecisions.Count}");
        text.WriteLine($"- Contradicting each other: {contradictions.Count}");
        text.WriteLine($"- Waiting for a held group: {waiting.Count}");
        text.WriteLine();
        if (plan.UnusedDecisions.Count > 0)
        {
            text.WriteLine($"### Decisions that match nothing in this data ({plan.UnusedDecisions.Count})");
            text.WriteLine();
            foreach (var key in plan.UnusedDecisions)
            {
                text.WriteLine($"- `{key}`");
            }

            text.WriteLine();
        }

        if (contradictions.Count > 0)
        {
            text.WriteLine($"### Decisions that contradict each other ({contradictions.Count})");
            text.WriteLine();
            foreach (var entry in contradictions)
            {
                text.WriteLine($"- `{entry.Key}`: decided different, but other decisions of same join the two");
            }

            text.WriteLine();
        }

        if (waiting.Count > 0)
        {
            text.WriteLine($"### Decisions that wait for a held group ({waiting.Count})");
            text.WriteLine();
            foreach (var entry in waiting)
            {
                var held = new[] { entry.Left, entry.Right }
                    .Where(x => x is { IsHeld: true })
                    .Select(x => $"`{x!.Key}`");
                text.WriteLine(
                    $"- `{entry.Key}`: it applies only when the held group {string.Join(" and ", held)} is decided `same`"
                );
            }

            text.WriteLine();
        }
    }

    static void WriteUnnamed(TextWriter text, IdentityPlan plan)
    {
        var unnamed = plan.Kinds.SelectMany(x => x.Unreadable).ToList();
        if (unnamed.Count == 0)
        {
            return;
        }

        text.WriteLine($"## Copies with no name ({unnamed.Count})");
        text.WriteLine();
        text.WriteLine("Nothing can be said of these, so they are left out of everything above.");
        text.WriteLine();
        foreach (
            var copy in unnamed
                .OrderBy(x => x.Collection, StringComparer.Ordinal)
                .ThenBy(x => x.DocumentId, StringComparer.Ordinal)
                .ThenBy(x => x.Path, StringComparer.Ordinal)
        )
        {
            var location = copy.Path.Length == 0 ? "" : $" ({copy.Path})";
            text.WriteLine($"- {Word(copy.Kind)} in {copy.Collection} {copy.DocumentId}{location}");
        }

        text.WriteLine();
    }

    /// <summary>The blank line that ends a list, after "None." when it had nothing in it.</summary>
    /// <summary>What an apply would do, beside the plan: what would stop it, the Rankings it would finalise, the planned result verified.</summary>
    static void WriteCutover(TextWriter text, PlannedCutover cutover)
    {
        text.WriteLine("## What an apply would do");
        text.WriteLine();
        if (cutover.Refusals.Count > 0)
        {
            text.WriteLine($"An apply would be refused ({cutover.Refusals.Count}):");
            text.WriteLine();
            foreach (var refusal in cutover.Refusals)
            {
                text.WriteLine($"- {refusal}");
            }

            text.WriteLine();
        }

        var unplaced = cutover.Rankings.Where(x => x.State == PlacingsState.Unplaced).ToList();
        var partial = cutover.Rankings.Where(x => x.State == PlacingsState.Partial).ToList();
        text.WriteLine(
            $"Rankings of Events that have ended: {cutover.Rankings.Count}, {unplaced.Count} with no placings (finalised by the apply, "
                + $"by the Results code; the ranks are computed then) and {partial.Count} with only some of their ranks (left alone)."
        );
        text.WriteLine();
        foreach (
            var ranking in unplaced
                .Concat(partial)
                .OrderBy(x => x.EventId, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
        )
        {
            var what =
                ranking.State == PlacingsState.Unplaced
                    ? "no placings, finalised at apply"
                    : "only some of its ranks, left alone";
            text.WriteLine($"- Ranking {ranking.Id} of Event {ranking.EventId} (\"{ranking.Name}\"): {what}");
        }

        if (unplaced.Count + partial.Count > 0)
        {
            text.WriteLine();
        }

        text.WriteLine("Verification of the planned result:");
        text.WriteLine();
        if (cutover.Checks.Count == 0)
        {
            text.WriteLine("- not made: an apply would be refused first.");
        }

        foreach (var check in cutover.Checks)
        {
            text.WriteLine($"- {(check.Passed ? "PASS" : "FAIL")} {check.Name}");
            foreach (var detail in check.Details)
            {
                text.WriteLine($"  - {detail}");
            }
        }

        text.WriteLine();
    }

    static void EndList(TextWriter text, int count)
    {
        if (count == 0)
        {
            text.WriteLine("None.");
        }

        text.WriteLine();
    }

    static void Row(TextWriter text, IdentityPlan plan, string label, Func<IdentityKindPlan, int> value)
    {
        text.WriteLine($"| {label} | {value(plan.Athletes)} | {value(plan.Horses)} |");
    }

    static int InPlace(IdentityKindPlan plan, params string[] collections)
    {
        return plan.Copies.Count(x => collections.Contains(x.Collection));
    }

    /// <summary>A link with its Setup row first, if it has one, and otherwise in the order of the keys.</summary>
    static SpellingLink Ordered(SpellingLink link)
    {
        var swap =
            link.One.IsSetupRow == link.Another.IsSetupRow
                ? string.CompareOrdinal(link.One.Key, link.Another.Key) > 0
                : link.Another.IsSetupRow;
        return swap ? new SpellingLink(link.Another, link.One, link.Class) : link;
    }

    /// <summary>A spelling in a line: its name, and what tells it from the others.</summary>
    static string Short(IdentitySpelling spelling)
    {
        var details = new List<string>();
        if (spelling.FeiId != null)
        {
            details.Add($"FEI ID {spelling.FeiId}");
        }

        if (spelling.LegacyId != null)
        {
            details.Add($"legacy id {spelling.LegacyId}");
        }

        details.Add(spelling.IsSetupRow ? SetupRow(spelling) : Plural(spelling.Copies.Count, "copy", "copies"));
        return $"\"{spelling.Label}\" ({string.Join(", ", details)})";
    }

    /// <summary>A row of Setup is told by its id when that is not the legacy id it is already told by: a Guid, so that two rows can be told apart.</summary>
    static string SetupRow(IdentitySpelling spelling)
    {
        var document = spelling.Copies[0].DocumentId;
        return document == spelling.LegacyId ? "Setup row" : $"Setup row {document}";
    }

    /// <summary>How many copies a spelling has and where, and how many rides they are: a Participation that two Rankings copy is one.</summary>
    static string Copies(IdentitySpelling spelling)
    {
        var places = Places
            .Select(x => (x.Label, Count: spelling.Copies.Count(y => x.Collections.Contains(y.Collection))))
            .Where(x => x.Count > 0)
            .Select(x => $"{x.Label} {x.Count}");
        var rides = spelling.Rides == 0 ? "" : $" ({Plural(spelling.Rides, "ride", "rides")})";
        return string.Join(", ", places) + rides;
    }

    static string Plural(int count, string one, string many)
    {
        return $"{count} {(count == 1 ? one : many)}";
    }

    static string Word(IdentityKind kind)
    {
        return kind == IdentityKind.Athlete ? "Athlete" : "Horse";
    }

    static string Title(MatchClass match)
    {
        return match switch
        {
            MatchClass.SameFeiId => "Same FEI ID",
            MatchClass.ExactName => "Same name",
            MatchClass.StrictTransliteration => "Same name in another script",
            MatchClass.SameLegacyId => "Same legacy id",
            _ => match.ToString(),
        };
    }

    static string Title(ReviewClass match)
    {
        return match switch
        {
            ReviewClass.Held => "Held groups",
            ReviewClass.Conflict => "Conflicts",
            ReviewClass.SpellingVariant => "Spelling variants",
            _ => "Near misses",
        };
    }

    static string Rule(MatchClass match)
    {
        return match switch
        {
            MatchClass.SameFeiId =>
                "The same FEI ID (an Athlete's compared as a number, a Horse's as text) and names that share something.",
            MatchClass.ExactName =>
                "The same name once trimmed, with its spaces collapsed and its case folded, across the name and the name in English of both.",
            MatchClass.StrictTransliteration => "The same name under the official Bulgarian-to-Latin transliteration.",
            _ => "The same legacy id other than 0, and names that share something.",
        };
    }

    static string Words(MatchClass match)
    {
        return match switch
        {
            MatchClass.SameFeiId => "same FEI ID",
            MatchClass.ExactName => "same name",
            MatchClass.StrictTransliteration => "same name in another script",
            MatchClass.SameLegacyId => "same legacy id",
            MatchClass.SpellingVariant => "spelling variant",
            MatchClass.NearMiss => "near miss",
            _ => "conflict",
        };
    }

    static string Intro(ReviewClass match)
    {
        return match switch
        {
            ReviewClass.Held =>
                "Held group. Two of its spellings disagree, so nothing in it is merged until you decide it as a whole: `same` makes all of it one, `different` keeps every spelling apart.",
            ReviewClass.Conflict =>
                "Conflict. The evidence disagrees: decide `same` if it is one, `different` if it is two.",
            ReviewClass.SpellingVariant => "Spelling variant. The names are the same letters spelled another way.",
            _ => "Near miss. The names are a letter or two apart.",
        };
    }
}
