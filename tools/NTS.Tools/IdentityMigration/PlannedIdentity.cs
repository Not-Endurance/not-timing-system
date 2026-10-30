namespace NTS.Tools.IdentityMigration;

/// <summary>
/// One Athlete or one Horse as the registry will have them (#636): the spellings that are one, by the automatic rules and by the
/// owner's decisions. The Setup rows in it are the registry's rows for it today, and when there are none a row is to be created.
/// </summary>
public sealed class PlannedIdentity
{
    internal PlannedIdentity(IReadOnlyList<IdentitySpelling> spellings, bool byDecision)
    {
        Spellings = spellings;
        ByDecision = byDecision;
        SetupRows = spellings.Count(x => x.IsSetupRow);
        Copies = spellings.Sum(x => x.Copies.Count);
    }

    public IReadOnlyList<IdentitySpelling> Spellings { get; }

    /// <summary>The rows of Setup that are this identity: more than one is a duplicate to merge, none is a row to create.</summary>
    public int SetupRows { get; }

    public int Copies { get; }

    /// <summary>Whether a decision of the owner joined spellings that the rules had not.</summary>
    public bool ByDecision { get; }
}
