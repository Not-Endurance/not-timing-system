using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// What <c>migrate-identities</c> would do (#636, ADR-0010): the copies it found, grouped by the rules of the matching service and
/// settled by the owner's decisions, for the Athletes and for the Horses. Building it reads nothing and writes nothing, so the same
/// copies and the same decisions make the same plan, in whatever order the documents came.
/// </summary>
public sealed class IdentityPlan
{
    /// <param name="copies">Every copy found, of both kinds.</param>
    /// <param name="events">The label of each Event (its name and dates) by the id the copies name it by.</param>
    /// <param name="decisions">What the owner has decided so far.</param>
    public static IdentityPlan Build(
        IReadOnlyList<IdentityCopy> copies,
        IReadOnlyDictionary<string, string> events,
        IdentityDecisions decisions
    )
    {
        return new IdentityPlan(
            new IdentityKindPlan(IdentityKind.Athlete, copies, events, decisions),
            new IdentityKindPlan(IdentityKind.Horse, copies, events, decisions),
            decisions
        );
    }

    IdentityPlan(IdentityKindPlan athletes, IdentityKindPlan horses, IdentityDecisions decisions)
    {
        Athletes = athletes;
        Horses = horses;
        var known = Kinds.SelectMany(x => x.Entries).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        UnusedDecisions = [.. decisions.ItemKeys.Where(x => !known.Contains(x)).Order(StringComparer.Ordinal)];
    }

    public IdentityKindPlan Athletes { get; }
    public IdentityKindPlan Horses { get; }

    public IReadOnlyList<IdentityKindPlan> Kinds => [Athletes, Horses];

    /// <summary>The items the decisions file decides that are no item of this data: a typing mistake, or data that has changed.</summary>
    public IReadOnlyList<string> UnusedDecisions { get; }
}
