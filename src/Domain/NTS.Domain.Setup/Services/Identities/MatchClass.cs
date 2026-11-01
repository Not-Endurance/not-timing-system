namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// The rule that decided how two copies of an Athlete or a Horse relate (#635, ADR-0010), so that a report can be grouped by it.
/// The first four are automatic: the copies are one. The last three are for a person to review, and the owner decides.
/// </summary>
public enum MatchClass
{
    /// <summary>The same FEI ID: an Athlete's compared as a number, a Horse's as text, and the names share something.</summary>
    SameFeiId = 1,

    /// <summary>The same name once trimmed, with its spaces collapsed and its case folded, across the name and the name in English of both.</summary>
    ExactName = 2,

    /// <summary>The same name under the official Bulgarian-to-Latin transliteration: one name in two scripts.</summary>
    StrictTransliteration = 3,

    /// <summary>The same legacy id, other than 0, and the names share something.</summary>
    SameLegacyId = 4,

    /// <summary>The same name by the looser key only: ю as yu or u, я as ya or ia, ц as ts or c, and the like.</summary>
    SpellingVariant = 5,

    /// <summary>Names one or two edits apart, or the same words in another order.</summary>
    NearMiss = 6,

    /// <summary>Evidence that disagrees: equal names under different FEI IDs, or a FEI ID or a legacy id that is shared by names that share nothing.</summary>
    Conflict = 7,
}
