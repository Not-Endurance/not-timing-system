namespace NTS.Contracts.Features.Console.Setup;

/// <summary>Why a row of the registry is taken for the same Athlete or Horse as the one that is being registered.</summary>
public enum SameRowReason
{
    /// <summary>It has the same name after trimming, collapsing spaces and case-folding, or the same name in the other script by the official transliteration.</summary>
    Name = 0,

    /// <summary>It has the same FEI ID: an Athlete's compared as a number, a Horse's trimmed and case-folded.</summary>
    FeiId = 1,

    /// <summary>Its name is a spelling of the same name by the looser key (ю as yu or u, я as ya or ia, ц as ts or c, and the like): it may be the same, and a person says.</summary>
    SimilarName = 2,
}

/// <summary>A row of the registry, the Tenant's own or another's, that an Athlete or a Horse about to be registered is a second copy of (ADR-0010).</summary>
public sealed class SameRow
{
    public SameRow(Guid id, string name, string? nameEnglish, string? feiId, bool isOwn, SameRowReason reason)
    {
        Id = id;
        Name = name;
        NameEnglish = nameEnglish;
        FeiId = feiId;
        IsOwn = isOwn;
        Reason = reason;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
    public string? FeiId { get; }

    /// <summary>Whether the Tenant the person acts in registered it: another Tenant's row is used by its id and is not edited.</summary>
    public bool IsOwn { get; }

    public SameRowReason Reason { get; }
}
