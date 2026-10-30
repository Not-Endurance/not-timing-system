namespace NTS.Tools.IdentityMigration;

/// <summary>What the owner says of a review item in the decisions file (#636). An item with no decision is still to be decided.</summary>
public enum IdentityDecision
{
    /// <summary>The copies are one Athlete or one Horse.</summary>
    Same = 1,

    /// <summary>The copies are two, and the item is settled.</summary>
    Different = 2,
}

/// <summary>
/// What kind of thing is left to the owner (#636, ADR-0010), the most blocking first: a group in which two copies disagree is held
/// whole; a pair of groups is a conflict, a spelling variant or a near-miss. A bulk rule in the decisions file settles one class.
/// </summary>
public enum ReviewClass
{
    Held = 1,
    Conflict = 2,
    SpellingVariant = 3,
    NearMiss = 4,
}
